using System;
using System.IO;
using System.Reflection;

namespace NMaier.SimpleDlna.Server
{
  internal sealed class IconHandler : IPrefixHandler
  {
    public string Prefix => "/icon/";

    public IResponse HandleRequest(IRequest req)
    {
      var resource = req.Path.Substring(Prefix.Length);
      var isPNG = resource.EndsWith(
        "png", StringComparison.OrdinalIgnoreCase);

      // Check if we have a custom icon to override the embedded resource
      var customIcon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CustomIcons", resource);
      if (File.Exists(customIcon))
      {
        return new FileResponse(
          HttpCode.Ok,
          isPNG ? "image/png" : "image/jpeg",
          new FileInfo(customIcon)
        );
      }

      // Return the embedded resource
      return new ResourceResponse(
        HttpCode.Ok,
        isPNG ? "image/png" : "image/jpeg",
        resource
        );
    }
  }
}
