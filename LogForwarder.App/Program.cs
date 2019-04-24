using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using LogForwarder.App.Backends;
using LogForwarder.App.Models;
using LogForwarder.App.Processors;
using LogForwarder.App.Watchers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogForwarder.App
{
  public class Program
  {
    private static IWatcher watcher;
    private static IProcessor processor;

    public static void Main(string[] args)
    {
      var options = new CommandlineOptions { };
      var parser = new Parser((settings) =>
      {
        settings.HelpWriter = Console.Out;
      });

      var result = parser.ParseArguments<CommandlineOptions>(args);

      if (result.Tag == ParserResultType.Parsed)
      {
        result.WithParsed((opts) =>
        {
          options = opts;
        });
        CreateProcessor(options);
        CreateWatcher(options);
        ScanDirectories(options);

        Console.WriteLine($"start watching for files is {options.Path} {options.Filter}");

        CreateWebHostBuilder(args).Build().Run();

        Console.WriteLine($"stop watching for files is {options.Path} {options.Filter}");
        watcher.Dispose();
        processor.Dispose();
      }
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
        .ConfigureServices((IServiceCollection obj) =>
        {
          obj.AddSingleton<IProcessorStatusReporter>(processor);
          obj.AddSingleton<IWatcherStatusReporter>(watcher);
        })
        .UseStartup<Startup>();

    private static void CreateProcessor(CommandlineOptions options)
    {
      switch (options.Mode)
      {
        case "files": 
          processor = new FileProcessor(
            options.MaxWorkers,
            CreateBackend(options),
            BuildScript<Data>(options));
          break;
        case "single_file": 
          processor = new SingleFileProcessor(
            options.MaxWorkers,
            CreateBackend(options),
            BuildScript<SingleLineData>(options),
            options.Path
          );
          break;
        default:
          throw new NotSupportedException(options.Mode);
      }
    }

    private static IBackend CreateBackend(CommandlineOptions options)
    {
      switch (options.Backend)
      {
        case "gcs":
          return new GCSBackend(options.DatabaseName, options.Dry);
      }
      throw new NotSupportedException(options.Backend);
    }

    private static ScriptRunner<Dictionary<string, string>> BuildScript<T>(CommandlineOptions options)
    {
      var scriptOptions = ScriptOptions.Default.
        WithImports("System.IO", "System.Linq").
        WithReferences(typeof(System.Linq.Enumerable).Assembly, typeof(System.IO.Path).Assembly, typeof(System.IO.DirectoryInfo).Assembly);

      var script = CSharpScript.Create<Dictionary<string, string>>(
        File.Exists(options.DataSourceScript) ? File.ReadAllText(options.DataSourceScript) : options.DataSourceScript,
        options: scriptOptions,
        globalsType: typeof(T));
      return script.CreateDelegate();
    }


    private static void CreateWatcher(CommandlineOptions options)
    {
      switch (options.Mode)
      {
        case "files": 
          watcher = new FileWatcher(options.Path, options.Filter);
          break;
        case "single_file": 
          watcher = new SingleFileWatcher(options.Path, options.Filter);
          break;
        default:
          throw new NotSupportedException(options.Mode);
      }
      watcher.OnFile += OnFile;
    }

    private static void OnFile(string path, string data)
    {
      processor.EnqueueItem(path, data);
    }

    private static void ScanDirectories(CommandlineOptions options)
    {
      if (!System.IO.Directory.Exists(options.Path))
      {
        throw new ArgumentException(options.Path);
      }
      var subDirs = System.IO.Directory.GetDirectories(options.Path, "*", SearchOption.AllDirectories);
      foreach (var currentDir in subDirs)
      {
        processor.EnqueueItem(currentDir, null);
      }
    }
  }
}