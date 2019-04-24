using System;
using LogForwarder.App.Models;

namespace LogForwarder.App.Processors
{
  internal interface IProcessor : IProcessorStatusReporter, IDisposable
  {
    void EnqueueItem(string fullPath, string data);
  }

  internal interface IProcessorFileRotate
  {
    void RotateFile(string oldPath, string newPath);
  }
}