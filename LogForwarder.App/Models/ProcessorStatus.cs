using System;

namespace LogForwarder.App.Models
{
  public class ProcessorStatus
  {
    public int EnqueuedItemCount;
    public int ElementsInQueue;
    public Worker[] Workers;
  }
}