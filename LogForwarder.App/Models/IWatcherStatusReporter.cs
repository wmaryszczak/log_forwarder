namespace LogForwarder.App.Models
{
  public interface IWatcherStatusReporter
  {
    WatcherStatus GetStatus();
  }
}