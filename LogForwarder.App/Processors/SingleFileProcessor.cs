using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LogForwarder.App.Atoms;
using LogForwarder.App.Backends;
using LogForwarder.App.Models;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;

namespace LogForwarder.App.Processors
{
  public class SingleFileProcessor : IProcessor
  {
    private const string ConfigFileName = "log_forwarder_single_file.config";
    private Worker[] workers;
    private CancellationTokenSource cancellationTokenSource;
    private CancellationToken cancellationToken;
    private readonly BlockingCollection<(string fullPath, string data)> items = new BlockingCollection<(string, string)>();
    private readonly IBackend backend;
    private readonly string basePath;
    private readonly SingleFileProcessorConfig config;
    private readonly ConcurrentDictionary<string, long> lastReadTstamps;
    private readonly ScriptRunner<Dictionary<string, string>> scriptRunner;
    private int EnqueuedItemCount;

    public SingleFileProcessor(int maxWorkers, IBackend backend, ScriptRunner<Dictionary<string, string>> scriptRunner, string basePath)
    {
      this.lastReadTstamps = new ConcurrentDictionary<string, long>();
      this.backend = backend;
      this.scriptRunner = scriptRunner;
      this.basePath = basePath;
      this.config = LoadConfig();
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

    public void EnqueueItem(string logFilePath, string data)
    {
      EnqueuedItemCount++;
      items.Add((logFilePath, data));
    }

    private void Work(object state)
    {
      var wrk = state as Worker;
      while (true)
      {
        wrk.IsWaiting = true;
        if (this.items.TryTake(out var item, 10, this.cancellationToken) && !string.IsNullOrEmpty(item.Item2))
        {
          wrk.IsWaiting = false;
          var tt = ProcessItem(item.Item1, item.Item2);
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

    private double ProcessItem(string logFilePath, string item)
    {
      Trace($"Peek {item.Substring(0, Math.Min(item.Length, 200))}");
      var tt = 0.0;
      
      if (this.cancellationToken.IsCancellationRequested)
      {
        return tt;
      }
      
      FileLogInfo fli = null;
      var dt = DateTime.UtcNow;

      try
      {
        using (fli = LoadFileFromLine(item, logFilePath, out var generationtstamp))
        {
          if (fli != null && CanPopulateFile(logFilePath, generationtstamp))
          {
            UpdateGenerationTimestamp(logFilePath, generationtstamp);
            this.backend.Send(fli);
          }
        } 
      }
      catch (Exception ex)
      {
        Error(ex);
      }
      finally
      {
        tt = (DateTime.UtcNow - dt).TotalMilliseconds;
      }

      return tt;
    }

    private void UpdateGenerationTimestamp(string fullPath, long generationtstamp)
    {
      this.lastReadTstamps.AddOrUpdate(fullPath, generationtstamp, (k,v) => generationtstamp);
    }

    private bool CanPopulateFile(string fullPath, long lineTstamp)
    {
      return (!this.config.LastReadDates.TryGetValue(fullPath, out var lastTstamp) || lineTstamp > lastTstamp);
    }

    private FileLogInfo LoadFileFromLine(string item, string logFilePath, out long timestamp)
    {
      var properties = item.Split('|');
      timestamp = long.Parse(properties[0]);

      var fi = new FileInfo(logFilePath);
      var opts = new Dictionary<string, string> { };
      var tmp = scriptRunner(new SingleLineData { FileInfo = fi, Options = opts, Properties = properties }).Result;

      var contentString = properties[9];
      var memStream = GenerateStreamFromString(contentString);
      var fli = new FileLogInfo(opts, new CryptoStream(memStream, new FromBase64Transform(), CryptoStreamMode.Read));
      return fli;
    }

    private Stream GenerateStreamFromString(string s)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(s);
        writer.Flush();
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
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
        SavePopulatedTStampToConfig();
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

    private SingleFileProcessorConfig LoadConfig()
    {
      var confFile = Path.Combine(this.basePath, ConfigFileName);
      if (File.Exists(confFile))
      {
        var text = File.ReadAllText(confFile);
        var config = JsonConvert.DeserializeObject<SingleFileProcessorConfig>(text);
      }

      return new SingleFileProcessorConfig();
    }

    private void SavePopulatedTStampToConfig()
    {
      var confFile = Path.Combine(this.basePath, ConfigFileName);
      this.config.LastReadDates = lastReadTstamps;

      var text = JsonConvert.SerializeObject(this.config);
      File.WriteAllText(confFile, text);
    }
  }

  public class SingleFileProcessorConfig
  {
    public ConcurrentDictionary<string, long> LastReadDates { get; set; }

    public SingleFileProcessorConfig()
    {
      this.LastReadDates = new ConcurrentDictionary<string, long>();
    }
  }
}