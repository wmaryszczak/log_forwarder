using System;

namespace LogForwarder.App.Models
{
  public class WatcherStatus
  {
    public string LastError;
    public DateTime? LastErrorTime;
    public DateTime LastEventTime;
    public int EventCount;
  }
}