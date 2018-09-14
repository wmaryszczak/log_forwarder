using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using log_forwarder.Backends;
using System.Threading;
using System.Globalization;
using System.Runtime.Loader;

namespace log_forwarder
{
  public class Data
  {
    public FileInfo FileInfo;
    public Dictionary<string, string> Options;
  }

  class Program
  {
    private static readonly AutoResetEvent closing = new AutoResetEvent(false);
    private static FileSystemWatcher watcher;
    private static FileProcessor processor;

    static void Main(string[] args)
    {
      AssemblyLoadContext.Default.Unloading += MethodInvokedOnSigTerm;
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
        Console.CancelKeyPress += new ConsoleCancelEventHandler(OnExit);
        Console.WriteLine($"start watching for files is {options.Path} {options.Filter}");
        while(!closing.WaitOne(10))
        {          
        }
        Console.WriteLine($"stop watching for files is {options.Path} {options.Filter}");
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        processor.Dispose();
      }
    }

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
      watcher = new FileSystemWatcher(options.Path, options.Filter);
      watcher.InternalBufferSize = 65536;
      watcher.NotifyFilter = NotifyFilters.FileName;
      watcher.EnableRaisingEvents = true;
      watcher.IncludeSubdirectories = true;
      watcher.Created += new FileSystemEventHandler(OnChanged);
      watcher.Error += new ErrorEventHandler(WatcherError);
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
      if(e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created)
      {
        Console.WriteLine($"start forward files from {e.FullPath} because of {e.ChangeType.ToString()}");
        var parent = Directory.GetParent(e.FullPath).ToString();
        processor.EnqueueItem(parent);
      }
    }

    private static void ScanDirectories(CommandlineOptions options)
    {
      if (!System.IO.Directory.Exists(options.Path))
      {
        throw new ArgumentException(options.Path);
      }
      var subDirs = System.IO.Directory.GetDirectories(options.Path, "*", SearchOption.AllDirectories);
      foreach(var currentDir in subDirs)
      {
        processor.EnqueueItem(currentDir);
      }
    }

    private static void WatcherError(object sender, ErrorEventArgs e)
    {
      Console.Error.WriteLine(e.GetException());
    }

    private static void OnExit(object sender, ConsoleCancelEventArgs args)
    {
      Console.WriteLine("Exit");
      closing.Set();
    }

    private static void MethodInvokedOnSigTerm(AssemblyLoadContext obj)
    {
      Console.WriteLine("Exit");
      closing.Set();
    }
  }
}