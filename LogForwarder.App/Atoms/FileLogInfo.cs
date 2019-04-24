using System;
using System.Collections.Generic;
using System.IO;

namespace LogForwarder.App.Atoms
{
  public class FileLogInfo : IDisposable
  {
    public string Bucket { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public string ContentEncoding { get; set; }
    public Stream Content { get; set; }

    public FileLogInfo(Dictionary<string, string> options, Stream content)
    {
      if (options.ContainsKey("bucket"))
      {
        Bucket = options["bucket"];
      }

      if (options.ContainsKey("filename"))
      {
        FileName = options["filename"];
      }

      if (options.ContainsKey("content_type"))
      {
        ContentType = options["content_type"];
      }

      if (options.ContainsKey("content_encoding"))
      {
        ContentEncoding = options["content_encoding"];
      }

      Content = content;
    }

    public FileLogInfo()
    {

    }  

    public void Dispose()
    {
      this.Content?.Dispose();
    }
  }
}