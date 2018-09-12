
using CommandLine;

class CommandlineOptions
{
  [Option('p', "path", Required = true, HelpText = "Path to directory with logs")]
  public string Path { get; set; }

  [Option('f', "filter", Required = false, HelpText = "Filter on file to observe", Default = "*.*")]
  public string Filter { get; set; }

  [Option('d', "db", Required = false, HelpText = "Database name (this is bucket name for GCS, database name for mongodb)")]
  public string DatabaseName { get; set; }

  [Option('s', "datascript", Required = false, HelpText = "C# Script to set up custom data for each backend (see readme.md)")]
  public string DataSourceScript { get; set; }

  [Option('b', "backend", Required = true, HelpText = "Determine backend for logs (gcs, mongodb)")]
  public string Backend { get; set; }

  [Option('c', "connection_string", Required = false, HelpText = "Determine connection string valid for backend")]
  public string ConnectionString { get; set; }

  [Option('w', "max_workers", Required = false, Default = 1, HelpText = "Maximum of workers which exports data in parallel")]
  public int MaxWorkers { get; set; }
  
  [Option('v', "verbose", Required = false, HelpText = "Open diagnostic channel")]
  public bool Verbose { get; set; }
}