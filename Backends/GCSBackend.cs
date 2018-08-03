using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;

namespace log_forwarder.Backends
{
  public class GCSBackend : IBackend
  {
    private readonly StorageClient client;
    private readonly string bucket;

    public GCSBackend(string bucket)
    {
      this.client = this.client = StorageClient.Create();
      this.bucket = bucket;
    }

    public Task SendAsync(string fullPath, Dictionary<string, string> options)
    {
      var bucketName = this.bucket ?? options["bucket"];
      var fileName = options["filename"];
      var ext = GetValue(options, "ext") ?? Path.GetExtension(fileName);
      var contentType = GetValue(options, "content_type") ?? Atoms.MimeType.GetMimeType(ext);
      return client.UploadObjectAsync(bucketName, fileName, contentType, File.OpenRead(fullPath));
    }

    public void Send(string fullPath, Dictionary<string, string> options)
    {
      var bucketName = this.bucket ?? options["bucket"];
      var fileName = options["filename"];
      var contentType = Atoms.MimeType.GetMimeType(GetValue(options, "content_type"));
      client.UploadObject(bucketName, fileName, contentType, File.OpenRead(fullPath));
    }

    private string GetValue(Dictionary<string, string> options, string key)
    {
      if(options.TryGetValue(key, out var val))
      {
        return val;
      }
      return null;
    }
  }
}