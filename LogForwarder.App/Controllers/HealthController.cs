using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogForwarder.App.Models;
using Microsoft.AspNetCore.Mvc;

namespace LogForwarder.App.Controllers
{
  [Route("[controller]")]
  [ApiController]
  public class HealthController : ControllerBase
  {
    private IProcessorStatusReporter processorStatusReporter;
    private IWatcherStatusReporter watcherStatusReporter;
    public HealthController(IProcessorStatusReporter processorStatusReporter, IWatcherStatusReporter watcherStatusReporter)
    {
      this.processorStatusReporter = processorStatusReporter;
      this.watcherStatusReporter = watcherStatusReporter;
    }
    // GET api/values
    [HttpGet]
    public ActionResult<Dictionary<string, object>> Get()
    {
      return new Dictionary<string, object> 
      { 
        { "file_watcher", this.watcherStatusReporter.GetStatus() },
        { "file_processor", this.processorStatusReporter.GetStatus() }
      };
    }
  }
}
