
using CommandLine;

class CommandlineOptions
{
  [Option('p', "path", Required = true, HelpText = "Path to directory with logs")]
  public string Path { get; set; }

  [Option('f', "filter", Required = false, HelpText = "Mask of files to include", Default = "*.*")]
  public string Filter { get; set; }
  
  [Option('d', "datasource", Required = false, HelpText = "Data source name")]
  public string DataSourceName { get; set; }

  [Option('s', "datascript", Required = false, HelpText = "C# Script to receive data source from argument FileSystemEventArgs")]
  public string DataSourceScript { get; set; }  
}