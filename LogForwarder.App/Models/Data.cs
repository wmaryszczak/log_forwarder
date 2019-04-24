using System;
using System.Collections.Generic;
using System.IO;

namespace LogForwarder.App.Models
{
  public class Data
  {
    public FileInfo FileInfo;
    public Dictionary<string, string> Options;
    public string Log { get; set; }
  }

  public class SingleLineData : Data
  {
    public string[] Properties { get; set; }
  }
}