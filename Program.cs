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

namespace log_forwarder
{
  public class Data
  {
    public FileInfo FileInfo;
    public Dictionary<string, string> Options;
  }

  class Program
  {
    private static IBackend backend;
    private static ScriptRunner<Dictionary<string, string>> scriptRunner;
    private static FileSystemWatcher watcher;

    static void Main(string[] args)
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
        CreateBackend(options);
        BuildScript(options);
        CreteWatcher(options);
        ScanDirectories(options);
        Console.WriteLine($"watching for files is {options.Path} {options.Filter}");
        Console.ReadKey();
        watcher.EnableRaisingEvents = false;
      }
    }

    private static void CreteWatcher(CommandlineOptions options)
    {
      watcher = new FileSystemWatcher(options.Path, options.Filter);
      watcher.InternalBufferSize = 1024 * 100;
      watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
      watcher.EnableRaisingEvents = true;
      watcher.IncludeSubdirectories = true;
      watcher.Changed += new FileSystemEventHandler(OnChanged);
      watcher.Created += new FileSystemEventHandler(OnChanged);
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
        var parent = Directory.GetParent(e.FullPath).ToString();

        foreach(var file in Directory.GetFiles(parent))
        {
          var fi = new FileInfo(file);
          if(fi.Length > 0)
          {
            var options = new Dictionary<string, string> { };
            var tmp = scriptRunner(new Data { FileInfo = fi, Options = options }).Result;
            Export(e.FullPath, options);
          }
        }
      }
    }

    private static void ScanDirectories(CommandlineOptions options)
    {
      if (!System.IO.Directory.Exists(options.Path))
      {
        throw new ArgumentException();
      }
      var dirs = new Stack<string>(20);
      dirs.Push(options.Path);
      while(dirs.Count > 0)
      {
        var currentDir = dirs.Pop();
        string[] subDirs;
        try
        {
          subDirs = System.IO.Directory.GetDirectories(currentDir);
        }
        catch (UnauthorizedAccessException e)
        {
          Console.WriteLine(e.Message);
          continue;
        }
        catch (System.IO.DirectoryNotFoundException e)
        {
          Console.WriteLine(e.Message);
          continue;
        }

        string[] files = null;
        try
        {
          files = System.IO.Directory.GetFiles(currentDir);
        }
        catch (UnauthorizedAccessException e)
        {

          Console.WriteLine(e.Message);
          continue;
        }
        catch (System.IO.DirectoryNotFoundException e)
        {
          Console.WriteLine(e.Message);
          continue;
        }
        foreach (string file in files)
        {
          try
          {
            var fi = new System.IO.FileInfo(file);
            var opts = new Dictionary<string, string> { };
            var tmp = scriptRunner(new Data { FileInfo = fi, Options = opts }).Result;
            Export(fi.FullName, opts);
          }
          catch (System.IO.FileNotFoundException e)
          {
            Console.WriteLine(e.Message);
            continue;
          }
        }
        foreach (string str in subDirs)
        {
          dirs.Push(str);
        }
      }
    }

    private static void Export(string fullPath, Dictionary<string, string> options)
    {
      backend.SendAsync(fullPath, options);
    }
  }
}