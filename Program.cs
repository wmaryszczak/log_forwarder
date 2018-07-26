using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace log_forwarder
{
  class Program
  {
    private static ScriptRunner<string> scriptRunner;
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
        var script = CSharpScript.Create<string>(options.DataSourceScript, globalsType: typeof(FileSystemEventArgs));
        scriptRunner = script.CreateDelegate();
        var watcher = new FileSystemWatcher(options.Path, options.Filter);
        watcher.InternalBufferSize = 1024 * 100;
        watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        watcher.EnableRaisingEvents = true;
        watcher.IncludeSubdirectories = true;
        watcher.Changed += new FileSystemEventHandler(OnChanged);
        watcher.Created += new FileSystemEventHandler(OnChanged);
        watcher.Deleted += new FileSystemEventHandler(OnChanged);
        watcher.Renamed += new RenamedEventHandler(OnRenamed);
        Console.WriteLine($"watching for files is {options.Path} {options.Filter}");
        Console.ReadKey();
        watcher.EnableRaisingEvents = false;
      }
    }

    private static void OnRenamed(object sender, RenamedEventArgs e)
    {
      Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
      if(e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created)
      {
        var parent = Directory.GetParent(e.FullPath).ToString();
        var dsName = scriptRunner(e).Result;
        Console.WriteLine(Directory.GetParent(e.FullPath));
        Console.WriteLine(string.Join('\n', Directory.GetFiles(parent)));
        Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
        Console.WriteLine("dsName: " + dsName);
        Console.WriteLine("dsName2: " + e.FullPath.Split('/')[4]);

        // Directory.Delete(parent, true);
      }
    }
  }
}