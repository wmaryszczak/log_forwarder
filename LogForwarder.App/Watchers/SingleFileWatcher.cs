using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using LogForwarder.App.Models;

namespace LogForwarder.App.Watchers
{
  public class SingleFileWatcher : IWatcher
  {
    private readonly object @lock = new object();

    private FileSystemWatcher watcher;
    public event Action<string, string> OnFile;
    private Exception lastError;
    private DateTime? lastErrorTime;
    private DateTime lastEventTime;
    private int eventCount;
    private ConcurrentDictionary<string, SingleFileInfo> logFiles;

    public SingleFileWatcher(string path, string filter)
    {
      this.logFiles = new ConcurrentDictionary<string, SingleFileInfo>();

      watcher = new FileSystemWatcher(path, filter);
      watcher.InternalBufferSize = 65536;
      watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
      watcher.EnableRaisingEvents = true;
      watcher.IncludeSubdirectories = true;
      watcher.Changed += new FileSystemEventHandler(OnChanged);
      watcher.Error += new ErrorEventHandler(WatcherError);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
      Console.WriteLine($"{e.FullPath} changed {e.ChangeType}");
      this.lastEventTime = DateTime.Now;
      ReadFile(e.FullPath);
    }

    private void ReadFile(string fullPath)
    {
      SingleFileInfo logFileInfo = null;
      lock(@lock)
      {
        try
        {
          logFileInfo = GetOrUpdateFileInfo(fullPath);
          if (logFileInfo.IsCurrentlyReading)
          {
            return;
          }
          logFileInfo.IsCurrentlyReading = true;
        }
        catch(FileNotFoundException)
        {
          return; // sometimes file rotate and does not at this moment
        }
      }

      try
      {
        var streamReader = logFileInfo.StreamReader;
        string line = streamReader.ReadLine();
        while (line != null)
        {
          eventCount++;
          OnFile(fullPath, line);
          line = streamReader.ReadLine();
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error while reading {fullPath}");
        throw ex;
      }
      finally
      {
        logFileInfo.IsCurrentlyReading = false;
        if (logFileInfo.CloseFileAfterRead)
        {
          logFileInfo.Dispose();
        }
      }
    }

    private SingleFileInfo GetOrUpdateFileInfo(string fullPath)
    {
      if (this.logFiles.TryGetValue(fullPath, out var logFileInfo))
      {
        if (logFileInfo.IsCurrentlyReading)
        {
          return logFileInfo;
        }
        
        var createDate = File.GetCreationTimeUtc(fullPath);
        if (logFileInfo.CreateDate != createDate)
        {
          logFileInfo.Dispose();
          logFileInfo = new SingleFileInfo(fullPath);
          this.logFiles.AddOrUpdate(fullPath, logFileInfo, (k,v) => logFileInfo);
          
          Console.WriteLine($"Reload stream for {fullPath} with creation date {logFileInfo.CreateDate.ToString()}");
        }
      }
      else
      {
        logFileInfo = new SingleFileInfo(fullPath);
        logFiles.TryAdd(fullPath, logFileInfo);
        Console.WriteLine($"Create stream for {fullPath}");
      }
      return logFileInfo;
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
      
      foreach (var kv in logFiles)
      {
        kv.Value.Dispose();
      }
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