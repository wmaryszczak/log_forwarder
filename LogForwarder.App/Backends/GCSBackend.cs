using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Storage.v1.Data;
using Google.Apis.Upload;
using Google.Cloud.Storage.V1;
using LogForwarder.App.Atoms;

namespace LogForwarder.App.Backends
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
      if(!dry)
      {
        this.client = this.client = StorageClient.Create();
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
      this.bucket = bucket;
      this.dry = dry;
    }

    public void Send(FileLogInfo file)
    {
      var bucketName = this.bucket ?? file.Bucket;

      if (string.IsNullOrEmpty(bucketName))
      {
        return;
      }

      var contentType = Atoms.MimeType.GetMimeType(file.ContentType);
      var contentEncoding = file.ContentEncoding;

      var obj = new Google.Apis.Storage.v1.Data.Object 
      { 
        Name = file.FileName,
        ContentType = contentType,
        ContentEncoding = contentEncoding,
        Bucket = bucketName,
      };
      if(dry)
      {
        Console.WriteLine($"DRY: push {file.FileName}");
        var sReader = new StreamReader(file.Content);
        var content = sReader.ReadToEnd();
        Console.WriteLine(content);
      }
      else
      {
        client.UploadObject(obj, file.Content, this.opts, progress);
      }
    }
  }
}