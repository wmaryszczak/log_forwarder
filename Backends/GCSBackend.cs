using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace log_forwarder.Backends
{
  public class GCSBackend : IBackend
  {
    public Task SendAsync(Stream stream, Dictionary<string, string> options)
    {
      // using()
    }
  }
}