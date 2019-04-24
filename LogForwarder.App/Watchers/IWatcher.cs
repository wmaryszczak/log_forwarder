using System;
using LogForwarder.App.Models;

namespace LogForwarder.App.Watchers
{
  internal interface IWatcher : IDisposable, IWatcherStatusReporter
  {
    event Action<string, string> OnFile; 
  }
}