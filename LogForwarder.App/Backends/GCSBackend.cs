using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Storage.v1.Data;
using Google.Apis.Upload;
using Google.Cloud.Storage.V1;

namespace log_forwarder.Backends
{
  public class GCSBackend : IBackend
  {
    private readonly StorageClient client;
    private readonly string bucket;
    private readonly bool dry;
    IProgress<IUploadProgress> progress;

    private readonly UploadObjectOptions opts = new UploadObjectOptions
    {
      Projection = Projection.NoAcl,
      ChunkSize = UploadObjectOptions.MinimumChunkSize * 10
    };


    public GCSBackend(string bucket, bool dry)
    {
      this.client = this.client = StorageClient.Create();
      this.bucket = bucket;
      this.dry = dry;
      this.progress = new Progress<IUploadProgress>(
        p =>
        {
          if (p.Exception != null)
          {
            Console.WriteLine(p.Exception);
          }
        }
      );
    }

    public void Send(string fullPath, Dictionary<string, string> options)
    {
      var bucketName = this.bucket ?? options["bucket"];
      var fileName = options["filename"];
      var contentType = Atoms.MimeType.GetMimeType(GetValue(options, "content_type"));
      var contentEncoding = GetValue(options, "content_encoding");
      var obj = new Google.Apis.Storage.v1.Data.Object 
      { 
        Name = fileName,
        ContentType = contentType,
        ContentEncoding = contentEncoding,
        Bucket = bucketName,        
      };
      if(dry)
      {
        Console.WriteLine($"DRY: push {fullPath}");
      }
      else
      {
        client.UploadObject(obj, File.OpenRead(fullPath), this.opts, progress);
        File.Delete(fullPath);
      }
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