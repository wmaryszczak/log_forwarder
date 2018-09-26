using System;
using System.Threading.Tasks;

namespace LogForwarder.App.Models
{
  public class Worker
  {
    public string Name;
    public bool IsWaiting;
    public int ProcessedItem;
    public DateTime LastProcessedTime;
    public double LastProcessedTimeTaken;
  }
}