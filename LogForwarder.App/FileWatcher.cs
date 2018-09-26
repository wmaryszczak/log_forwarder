using System;
using System.IO;
using LogForwarder.App.Models;

namespace LogForwarder.App
{
  public class FileWatcher : IWatcherStatusReporter, IDisposable
  {
    private FileSystemWatcher watcher;
    public event Action<string> OnFile;
    private Exception lastError;
    private DateTime? lastErrorTime;
    private DateTime lastEventTime;
    private int eventCount;


    public FileWatcher(string path, string filter)
    {
      watcher = new FileSystemWatcher(path, filter);
      watcher.InternalBufferSize = 65536;
      watcher.NotifyFilter = NotifyFilters.FileName;
      watcher.EnableRaisingEvents = true;
      watcher.IncludeSubdirectories = true;
      watcher.Created += new FileSystemEventHandler(OnChanged);
      watcher.Error += new ErrorEventHandler(WatcherError);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
      if (e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created)
      {
        this.eventCount++;
        this.lastEventTime = DateTime.Now;
        Console.WriteLine($"start forward files from {e.FullPath} because of {e.ChangeType.ToString()}");
        var parent = Directory.GetParent(e.FullPath).ToString();
        if(this.OnFile != null)
        {
          this.OnFile(parent);
        }
      }
    }

    private void WatcherError(object sender, ErrorEventArgs e)
    {
      var ex = e.GetException();
      Console.Error.WriteLine(ex);
      this.lastError = ex;
      this.lastErrorTime = DateTime.Now;
    }

    public void Dispose()
    {
      this.watcher.EnableRaisingEvents = false;
      this.watcher.Dispose();
    }

    public WatcherStatus GetStatus()
    {
      return new WatcherStatus
      {
        LastError = this.lastError?.Message,
        LastErrorTime = this.lastErrorTime,
        LastEventTime = this.lastEventTime,
        EventCount = this.eventCount,
      };
    }
  }
}