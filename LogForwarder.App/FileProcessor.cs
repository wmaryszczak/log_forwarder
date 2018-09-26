using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogForwarder.App.Backends;
using LogForwarder.App.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace LogForwarder.App
{
  class FileProcessor : IProcessorStatusReporter, IDisposable
  {
    private Worker[] workers;
    private CancellationTokenSource cancellationTokenSource;
    private CancellationToken cancellationToken;
    private readonly BlockingCollection<string> items = new BlockingCollection<string>();
    private readonly IBackend backend;
    private readonly ScriptRunner<Dictionary<string, string>> scriptRunner;
    private int EnqueuedItemCount;

    public FileProcessor(int maxWorkers, IBackend backend, ScriptRunner<Dictionary<string, string>> scriptRunner)
    {
      this.scriptRunner = scriptRunner;
      this.backend = backend;
      InitBackgroundThread(maxWorkers);
    }

    private void InitBackgroundThread(int maxWorkers)
    {
      this.workers = new Worker[maxWorkers];
      this.cancellationTokenSource = new CancellationTokenSource();
      this.cancellationToken = this.cancellationTokenSource.Token;

      for (int i = 0; i < maxWorkers; i++)
      {
        var worker = new Worker { Name = $"#{i}wrk" };
        Task.Factory.StartNew(Work, worker, this.cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        workers[i] = worker;
      }
    }

    public void EnqueueItem(string FileName)
    {
      EnqueuedItemCount++;
      items.Add(FileName);
    }

    private void Work(object state)
    {
      var wrk = state as Worker;
      while (true)
      {
        wrk.IsWaiting = true;
        if (this.items.TryTake(out var item, 10, this.cancellationToken) && !string.IsNullOrEmpty(item))
        {
          wrk.IsWaiting = false;
          var tt = ProcessItem(item);
          wrk.ProcessedItem++;
          wrk.LastProcessedTime = DateTime.Now;
          wrk.LastProcessedTimeTaken = tt;
        }
        if (this.cancellationToken.IsCancellationRequested)
        {
          break;
        }
      }
    }

    private double ProcessItem(string item)
    {
      Trace($"Peek {item}");
      var tt = 0.0;
      string[] files = null;
      var processedFilesCounter = 0;
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
        return tt;
      }
      catch (System.IO.DirectoryNotFoundException e)
      {
        Error(e);
        return tt;
      }
      if (files == null || files.Length == 0)
      {
        return tt;
      }
      if (!files.Any(f => Path.GetExtension(f).EndsWith("complete")))
      {
        Trace($"skipping {item} because is not ready for sending files.");
        return tt;
      }
      var dt = DateTime.UtcNow;
      foreach (string file in files)
      {
        if (this.cancellationToken.IsCancellationRequested)
        {
          break;
        }
        try
        {
          var fi = new System.IO.FileInfo(file);
          if (fi.Length > 0 && !fi.Attributes.HasFlag(FileAttributes.Hidden))
          {
            var opts = new Dictionary<string, string> { };
            var tmp = scriptRunner(new Data { FileInfo = fi, Options = opts }).Result;
            Export(fi.FullName, opts);
            processedFilesCounter++;
          }
        }
        catch (Exception e)
        {
          Error(e);
          exportComplete = false;
          continue;
        }
      }
      tt = (DateTime.UtcNow - dt).TotalMilliseconds;
      var msg = $"{processedFilesCounter}/{files.Length} have been exported in [{tt.ToString(CultureInfo.InvariantCulture)}] ms";
      try
      {
        if (exportComplete)
        {
          Directory.Delete(item, true);
          Trace($"{msg} and cleaned");
        }
        else
        {
          var count = System.IO.Directory.GetFiles(item).Count(f => !Path.GetExtension(f).EndsWith("complete"));
          Trace($"{msg} and {count} files left incomplete");
        }
      }
      catch (Exception ex)
      {
        Error(ex);
      }
      return tt;
    }

    private void Export(string fullPath, Dictionary<string, string> options)
    {
      backend.Send(fullPath, options);
    }

    public ProcessorStatus GetStatus()
    {
      return new ProcessorStatus 
      { 
        EnqueuedItemCount = this.EnqueuedItemCount,
        ElementsInQueue = this.items.Count,
        Workers = this.workers
      };
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