using System;
using System.IO;

namespace LogForwarder.App.Watchers
{
  public class SingleFileInfo : IDisposable
  {
    public StreamReader StreamReader { get; set; }
    public DateTime CreateDate { get; set; }
    public bool CloseFileAfterRead { get; set; }
    public bool IsCurrentlyReading { get; set; }

    public SingleFileInfo(string filePath)
    {
      var stream = File.OpenRead(filePath);
      StreamReader = new StreamReader(stream);
      CreateDate = File.GetCreationTimeUtc(filePath);
      IsCurrentlyReading = false;
      CloseFileAfterRead = false;
    }

    public void Dispose()
    {
      if (IsCurrentlyReading)
      {
        CloseFileAfterRead = true;
      }
      else
      {
        this.StreamReader?.Dispose();
      }
    }
  }
}