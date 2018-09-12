using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;

namespace log_forwarder.Backends
{
  public class DryBackend : IBackend
  {
    public void Send(string fullPath, Dictionary<string, string> options)
    {
      foreach (var kvp in options)
      {
        Console.WriteLine($"{kvp.Key} = {kvp.Value}");
      }
    }
  }
}