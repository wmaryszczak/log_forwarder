using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;

namespace log_forwarder.Backends
{
  public class GCSBackend : IBackend
  {
    private readonly StorageClient client;

    public GCSBackend()
    {
      this.client = this.client = StorageClient.Create();
    }

    public Task SendAsync(Stream stream, Dictionary<string, string> options)
    {
      var bucketName = options["bucket"];
      var fileName = options["filename"];
      var contentType = options["content_type"];
      return client.UploadObjectAsync(bucketName, fileName, contentType, stream);
    }
  }
}