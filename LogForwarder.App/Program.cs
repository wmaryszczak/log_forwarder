using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using LogForwarder.App.Backends;
using LogForwarder.App.Models;
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
    private static FileWatcher watcher;
    private static FileProcessor processor;

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
      processor = new FileProcessor(
        options.MaxWorkers,
        CreateBackend(options),
        BuildScript(options)
      );
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

    private static ScriptRunner<Dictionary<string, string>> BuildScript(CommandlineOptions options)
    {
      var scriptOptions = ScriptOptions.Default.
        WithImports("System.IO", "System.Linq").
        WithReferences(typeof(System.Linq.Enumerable).Assembly, typeof(System.IO.Path).Assembly, typeof(System.IO.DirectoryInfo).Assembly);

      var script = CSharpScript.Create<Dictionary<string, string>>(
        File.Exists(options.DataSourceScript) ? File.ReadAllText(options.DataSourceScript) : options.DataSourceScript,
        options: scriptOptions,
        globalsType: typeof(Data));
      return script.CreateDelegate();
    }


    private static void CreateWatcher(CommandlineOptions options)
    {
      watcher = new FileWatcher(options.Path, options.Filter);
      watcher.OnFile += OnFile;
    }

    private static void OnFile(string path)
    {
      processor.EnqueueItem(path);
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
        processor.EnqueueItem(currentDir);
      }
    }
  }
}