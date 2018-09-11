using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log_forwarder.Backends;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace log_forwarder
{
  class FileProcessor : IDisposable
  {
    private Task[] workers;
    private CancellationTokenSource cancellationTokenSource;
    private CancellationToken cancellationToken;
    private readonly BlockingCollection<string> items = new BlockingCollection<string>();
    private readonly IBackend backend;
    private readonly ScriptRunner<Dictionary<string, string>> scriptRunner;

    public FileProcessor(int maxWorkers, IBackend backend, ScriptRunner<Dictionary<string, string>> scriptRunner)
    {
      this.scriptRunner = scriptRunner;
      this.backend = backend;
      InitBackgroundThread(maxWorkers);
    }

    private void InitBackgroundThread(int maxWorkers)
    {
      this.workers = new Task[maxWorkers];
      this.cancellationTokenSource = new CancellationTokenSource();
      this.cancellationToken = this.cancellationTokenSource.Token;

      for (int i = 0; i < maxWorkers; i++)
      {
        this.workers[i] = Task.Factory.StartNew(() =>
        {
          Work();
        }, this.cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
      }
    }

    public void EnqueueItem(string FileName)
    {
      items.Add(FileName);
    }

    private void Work()
    {
      while (true)
      {
        if (this.items.TryTake(out var item, 10, this.cancellationToken) && !string.IsNullOrEmpty(item))
        {
          ProcessItem(item);
        }
        if (this.cancellationToken.IsCancellationRequested)
        {
          break;
        }
      }
    }

    private void ProcessItem(string item)
    {
      Trace($"Peek {item}");
      string[] files = null;
      var exportedFilesCounter = 0;
      var exportComplete = true;
      try
      {
        if (Directory.Exists(item))
        {
          files = System.IO.Directory.GetFiles(item);
        }
      }
      catch (UnauthorizedAccessException e)
      {
        Error(e);
        return;
      }
      catch (System.IO.DirectoryNotFoundException e)
      {
        Error(e);
        return;
      }
      if (files == null || files.Length == 0)
      {
        return;
      }
      if (!files.Any(f => Path.GetExtension(f).EndsWith("complete")))
      {
        Trace($"skipping {item} because is not ready for sending files.");
        return;
      }
      var dt = DateTime.UtcNow;
      foreach (string file in files)
      {
        try
        {
          var fi = new System.IO.FileInfo(file);
          if (fi.Length > 0)
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
          Error(e);
          exportComplete = false;
          continue;
        }
      }
      var tt = (DateTime.UtcNow - dt).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
      var msg = $"{exportedFilesCounter}/{files.Length} have been exported in [{tt}] ms";
      try
      {
        if (exportComplete)
        {
          Directory.Delete(item, true);
          Trace($"{msg} and cleaned");
        }
        else
        {
          Trace($"{msg} and left incomplete");
        }
      }
      catch (Exception ex)
      {
        Error(ex);
      }
    }

    private void Export(string fullPath, Dictionary<string, string> options)
    {
      backend.Send(fullPath, options);
    }

    #region IDisposable Members

    public void Dispose()
    {
      try
      {
        this.items.CompleteAdding();
        this.cancellationTokenSource.Cancel();
      }
      catch (AggregateException ex)
      {
        Error(ex.Flatten().InnerException.ToString());
      }
      finally
      {
        if(this.items != null)
        {
          this.items.Dispose();
        }
        if(this.cancellationTokenSource != null)
        {
          this.cancellationTokenSource.Dispose();
        }
      }
    }
    #endregion

    private void Trace(string message)
    {
      Console.WriteLine($"#{Thread.CurrentThread.ManagedThreadId}: {message}");
    }

    private void Error(Exception ex)
    {
      Console.Error.WriteLine($"#{Thread.CurrentThread.ManagedThreadId}: {ex}");
    }

    private void Error(string message)
    {
      Console.Error.WriteLine($"#{Thread.CurrentThread.ManagedThreadId}: {message}");
    }
  }
}