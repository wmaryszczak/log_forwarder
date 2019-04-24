using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LogForwarder.App.Atoms;

namespace LogForwarder.App.Backends
{
  public interface IBackend
  {
    void Send(FileLogInfo file);
  }
}