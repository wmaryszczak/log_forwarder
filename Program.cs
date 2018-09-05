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
    private static IBackend backend;
    private static ScriptRunner<Dictionary<string, string>> scriptRunner;
    private static FileSystemWatcher watcher;

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
        CreateBackend(options);
        BuildScript(options);
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
      }
    }

    private static void CreateWatcher(CommandlineOptions options)
    {
      watcher = new FileSystemWatcher(options.Path, options.Filter);
      watcher.InternalBufferSize = 1024 * 100;
      watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
      watcher.EnableRaisingEvents = true;
      watcher.IncludeSubdirectories = true;
      watcher.Changed += new FileSystemEventHandler(OnChanged);
      watcher.Created += new FileSystemEventHandler(OnChanged);
      watcher.Error += new ErrorEventHandler(WatcherError);
    }

    private static void BuildScript(CommandlineOptions options)
    {
      var scriptOptions = ScriptOptions.Default.
        WithImports("System.IO", "System.Linq").
        WithReferences(typeof(System.Linq.Enumerable).Assembly, typeof(System.IO.Path).Assembly, typeof(System.IO.DirectoryInfo).Assembly);

      var script = CSharpScript.Create<Dictionary<string, string>>(
        File.Exists(options.DataSourceScript) ? File.ReadAllText(options.DataSourceScript) : options.DataSourceScript,
        options: scriptOptions,
        globalsType: typeof(Data));
      scriptRunner = script.CreateDelegate();
    }

    private static void CreateBackend(CommandlineOptions options)
    {
      switch (options.Backend)
      {
        case "gcs":
          backend = new GCSBackend(options.DatabaseName);
          break;
        case "dry":
          backend = new DryBackend();
          break;
        default:
          throw new NotSupportedException(options.Backend);
      }
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
      if(e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created)
      {
        Console.WriteLine($"start forward files from {e.FullPath} because of {e.ChangeType.ToString()}");
        var parent = Directory.GetParent(e.FullPath).ToString();
        ExportAllFiles(parent);
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
        ExportAllFiles(currentDir);
        Console.Error.WriteLine($"skipping {currentDir} because is not ready for sending files.");
      }
    }

    private static void ExportAllFiles(string currentDir)
    {
      string[] files = null;
      var exportedFilesCounter = 0;
      var exportComplete = true;
      try
      {
        if(Directory.Exists(currentDir))
        {
          files = System.IO.Directory.GetFiles(currentDir);
        }
      }
      catch (UnauthorizedAccessException e)
      {
        Console.Error.WriteLine(e);
        return;
      }
      catch (System.IO.DirectoryNotFoundException e)
      {
        Console.Error.WriteLine(e);
        return;
      }
      if(!files.Any(f => Path.GetExtension(f).EndsWith("complete")))
      {
        Console.Error.WriteLine($"skipping {currentDir} because is not ready for sending files.");
        return;
      }
      var dt = DateTime.UtcNow;
      foreach (string file in files)
      {
        try
        {
          var fi = new System.IO.FileInfo(file);
          if(fi.Length > 0)
          {
            var opts = new Dictionary<string, string> { };
            var tmp = scriptRunner(new Data { FileInfo = fi, Options = opts }).Result;
            Export(fi.FullName, opts);
            File.Delete(fi.FullName);
            exportedFilesCounter++;
          }
        }
        catch (System.IO.FileNotFoundException e)
        {
          Console.Error.WriteLine(e);
          exportComplete = false;
          continue;
        }
      }
      var tt = (DateTime.UtcNow - dt).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
      Console.Write($"{exportedFilesCounter}/{files.Length} have been exported in [{tt}] ms");
      try
      {
        if(exportComplete)
        {
          Directory.Delete(currentDir, true);
          Console.WriteLine($" and cleaned");
        }
        else
        {
          Console.WriteLine($" and left incomplete");
        }
      }
      catch(Exception ex)
      {
        Console.Error.WriteLine(ex);
      }
    }

    private static void WatcherError(object sender, ErrorEventArgs e)
    {
      Console.Error.WriteLine(e.GetException());
    }


    private static void Export(string fullPath, Dictionary<string, string> options)
    {
      backend.Send(fullPath, options);
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