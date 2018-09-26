namespace LogForwarder.App.Models
{
  public interface IProcessorStatusReporter
  {
    ProcessorStatus GetStatus();
  }
}