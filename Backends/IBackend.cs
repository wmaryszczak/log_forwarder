using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace log_forwarder.Backends
{
  public interface IBackend
  {
    Task SendAsync(Stream stream, Dictionary<string, string> options);
  }
}