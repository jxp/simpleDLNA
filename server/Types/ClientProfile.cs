using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NMaier.SimpleDlna.Server
{
  /// <summary>
  ///   What a client (renderer) told us about itself via its device
  ///   description and ConnectionManager GetProtocolInfo Sink list.
  /// </summary>
  public sealed class ClientProfile
  {
    public ClientProfile(IPAddress address, Uri location, string server,
      string deviceType, string friendlyName, string manufacturer,
      string modelName, IList<DlnaProtocolInfo> sinkProtocols)
    {
      Address = address;
      Location = location;
      Server = server;
      DeviceType = deviceType;
      FriendlyName = friendlyName;
      Manufacturer = manufacturer;
      ModelName = modelName;
      SinkProtocols = sinkProtocols ?? new DlnaProtocolInfo[0];
      Discovered = DateTime.Now;
    }

    public IPAddress Address { get; }

    public string DeviceType { get; }

    public DateTime Discovered { get; }

    public string FriendlyName { get; }

    public bool HasSinkProtocols => SinkProtocols.Count > 0;

    public Uri Location { get; }

    public string Manufacturer { get; }

    public string ModelName { get; }

    /// <summary>
    ///   SSDP SERVER header, if any
    /// </summary>
    public string Server { get; }

    public IList<DlnaProtocolInfo> SinkProtocols { get; }

    /// <summary>
    ///   Whether the client advertised support for this MIME type (and DLNA
    ///   profile, when both sides specify one).
    /// </summary>
    public bool CanPlay(string mime, string profileName = null)
    {
      return SinkProtocols.Any(p => p.Matches(mime, profileName));
    }

    public override string ToString()
    {
      return $"{FriendlyName} ({Manufacturer} {ModelName}) at {Address}";
    }
  }
}
