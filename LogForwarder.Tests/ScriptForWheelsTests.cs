using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogForwarder.App;
using LogForwarder.App.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Xunit;

namespace LogForwarder.Tests
{
  public class ScriptForWheelsTests
  {
    private readonly ScriptRunner<Dictionary<string, string>> scriptRunner;

    public ScriptForWheelsTests()
    {
      var scriptOptions = ScriptOptions.Default.
        WithImports("System.IO", "System.Linq").
        WithEmitDebugInformation(true).
        WithReferences(typeof(System.Linq.Enumerable).Assembly, typeof(System.IO.Path).Assembly, typeof(System.IO.DirectoryInfo).Assembly);
      var path = System.IO.Path.Combine(GetSlnDir(), "LogForwarder.App", "scripts", "wheels_gcs.cs.txt");
      Console.WriteLine(path);
      var script = CSharpScript.Create<Dictionary<string, string>>(
        File.ReadAllText(path),
        options: scriptOptions,
        globalsType: typeof(Data));
      this.scriptRunner = script.CreateDelegate();
    }

    private string GetSlnDir()
    {
      var currDir = Environment.CurrentDirectory;
      while(Directory.GetParent(currDir) != null && !Directory.EnumerateFiles(currDir, "*.sln").Any())
      {
        currDir = Directory.GetParent(currDir).FullName;
      }
      return currDir;
    }

    [Fact]
    public void Should_Execute_Script()
    {
      var fi = new FileInfo("/var/log/resfinity/wheels/anixe-car-ftidrive/4858958814782733707/5519216858885726970.4865352600837823080.import_update_stations.request.txt.gzip");
      var opts = new Dictionary<string, string> { };
      var tmp = scriptRunner(new Data { FileInfo = fi, Options = opts }).Result;
      Assert.Equal("gzip", opts["content_encoding"]);
      Assert.Equal("anixe-car-ftidrive", opts["bucket"]);
      Assert.Equal("txt", opts["content_type"]);
      Assert.Equal("4858958814782733707/5519216858885726970.4865352600837823080.import_update_stations.request.txt.gzip", opts["filename"]);
    }

    [Fact]
    public void Should_Execute_Script2()
    {
      var fi = new FileInfo("/var/log/resfinity/wheels/anixe-car-ftidrive/4825743842524527130/Anixe.RuleMatchers.ex.5240808385290718874.json.1.1.1");
      var opts = new Dictionary<string, string> { };
      var tmp = scriptRunner(new Data { FileInfo = fi, Options = opts }).Result;
      Assert.Equal("anixe-car-ftidrive", opts["bucket"]);
      Assert.Equal("txt", opts["content_type"]);
      Assert.Equal("4825743842524527130/Anixe.RuleMatchers.ex.5240808385290718874.json.1.1.1", opts["filename"]);
    }

    [Fact]
    public void Should_Execute_Script3()
    {
      var fi = new FileInfo("/var/log/resfinity/wheels/anixe-car-ftidrive/4898091677679622523/5205389114296564851.5524935925715209161.get.response.json.1.1.1.1.1.1");
      var opts = new Dictionary<string, string> { };
      var tmp = scriptRunner(new Data { FileInfo = fi, Options = opts }).Result;
      Assert.Equal("anixe-car-ftidrive", opts["bucket"]);
      Assert.Equal("txt", opts["content_type"]);
      Assert.Equal("4898091677679622523/5205389114296564851.5524935925715209161.get.response.json.1.1.1.1.1.1", opts["filename"]);
    }

  }
}