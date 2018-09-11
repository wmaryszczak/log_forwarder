using System;
using System.Collections.Generic;

namespace log_forwarder.Atoms
{
  public static class MimeType
  {
    private static Dictionary<string, string> mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      {"txt", "text/plain"},
      {"json", "application/json"},
      {"xml", "text/xml"},
      {"csv", "text/csv"},
      {"ion", "text/ion"},
      {".htm", "text/html"},
      {".html", "text/html"},
      {".ion", "text/ion"},
      {".csv", "text/csv"},
    };

    public static string GetMimeType(string extension)
    {
      if(mapping.TryGetValue(extension, out var val))
      {
        return val;
      }
      return extension;
    }
  }
}
