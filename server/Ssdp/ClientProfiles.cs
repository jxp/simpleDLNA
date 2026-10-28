using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;
using System.Xml;
using NMaier.SimpleDlna.Utilities;

namespace NMaier.SimpleDlna.Server.Ssdp
{
  /// <summary>
  ///   Discovers MediaRenderers on the network and asks each one, via its
  ///   ConnectionManager GetProtocolInfo action, what it can play.
  ///   Profiles are keyed by IP so they can be matched to HTTP requests.
  /// </summary>
  internal sealed class ClientProfiles : Logging
  {
    private const string CONNECTION_MANAGER_PREFIX =
      "urn:schemas-upnp-org:service:ConnectionManager:";

    private const int HTTP_TIMEOUT = 5000;

    private const string RENDERER_PREFIX =
      "urn:schemas-upnp-org:device:MediaRenderer:";

    private const string RENDERER_TYPE =
      "urn:schemas-upnp-org:device:MediaRenderer:1";

    private const int SEARCH_MX = 2;

    private const int SSDP_PORT = 1900;

    public static readonly ClientProfiles Instance = new ClientProfiles();

    private static readonly TimeSpan probeInterval = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan refreshInterval =
      TimeSpan.FromMinutes(30);

    private static readonly TimeSpan retryInterval = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan searchInterval = TimeSpan.FromMinutes(1);

    private static readonly IPEndPoint ssdpEndp =
      new IPEndPoint(IPAddress.Parse("239.255.255.250"), SSDP_PORT);

    // Location -> time at which it may be fetched again
    private readonly ConcurrentDictionary<Uri, DateTime> fetches =
      new ConcurrentDictionary<Uri, DateTime>();

    private readonly ConcurrentDictionary<IPAddress, ClientProfile> profiles =
      new ConcurrentDictionary<IPAddress, ClientProfile>();

    // "local>target" -> time at which it may be searched again
    private readonly ConcurrentDictionary<string, DateTime> searches =
      new ConcurrentDictionary<string, DateTime>();

    private ClientProfiles()
    {
    }

    public ICollection<ClientProfile> All => profiles.Values;

    private static XmlDocument LoadXml(Stream stream)
    {
      var settings = new XmlReaderSettings
      {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null
      };
      var doc = new XmlDocument {XmlResolver = null};
      using (var reader = XmlReader.Create(stream, settings)) {
        doc.Load(reader);
      }
      return doc;
    }

    private static string ChildText(XmlNode node, string name)
    {
      return node?.SelectSingleNode($"*[local-name() = '{name}']")
        ?.InnerText.Trim();
    }

    private static HttpWebRequest CreateRequest(Uri uri)
    {
      var request = (HttpWebRequest)WebRequest.Create(uri);
      request.Timeout = HTTP_TIMEOUT;
      request.ReadWriteTimeout = HTTP_TIMEOUT;
      request.KeepAlive = false;
      request.Proxy = null;
      request.UserAgent = HttpServer.Signature;
      return request;
    }

    private static Headers ParseSsdp(byte[] data, out string proto)
    {
      var headers = new Headers();
      using (var reader = new StreamReader(
        new MemoryStream(data), Encoding.ASCII)) {
        proto = reader.ReadLine()?.Trim() ?? string.Empty;
        for (var line = reader.ReadLine();
          line != null;
          line = reader.ReadLine()) {
          line = line.Trim();
          if (string.IsNullOrEmpty(line)) {
            break;
          }
          var parts = line.Split(new[] {':'}, 2);
          if (parts.Length == 2) {
            headers[parts[0]] = parts[1].Trim();
          }
        }
      }
      return headers;
    }

    /// <summary>
    ///   An SSDP NOTIFY or M-SEARCH response was seen. Queries the device if
    ///   it is a renderer we have not (recently) queried.
    /// </summary>
    internal void Announce(IPAddress address, string type, string location,
      string server)
    {
      if (type == null ||
          !type.StartsWith(RENDERER_PREFIX, StringComparison.OrdinalIgnoreCase)) {
        return;
      }
      Uri uri;
      if (!Uri.TryCreate(location, UriKind.Absolute, out uri) ||
          uri.Scheme != Uri.UriSchemeHttp) {
        return;
      }
      if (!Claim(fetches, uri, refreshInterval)) {
        return;
      }
      DebugFormat("Renderer announced at {0}: {1}", address, uri);
      ThreadPool.QueueUserWorkItem(_ => Fetch(address, uri, server));
    }

    public ClientProfile Get(IPAddress address)
    {
      ClientProfile rv;
      return profiles.TryGetValue(address, out rv) ? rv : null;
    }

    /// <summary>
    ///   A client we know nothing about sent an HTTP request; ask it directly
    ///   (unicast M-SEARCH) whether it is a renderer.
    /// </summary>
    internal void Probe(IPAddress remote, IPAddress local)
    {
      if (IPAddress.IsLoopback(remote) || profiles.ContainsKey(remote)) {
        return;
      }
      Search(local, new IPEndPoint(remote, SSDP_PORT), probeInterval);
    }

    /// <summary>
    ///   Multicast an M-SEARCH for renderers from the given local address.
    /// </summary>
    internal void Search(IPAddress local)
    {
      Search(local, ssdpEndp, searchInterval);
    }

    private void Search(IPAddress local, IPEndPoint target,
      TimeSpan minInterval)
    {
      if (local.AddressFamily != target.AddressFamily) {
        return;
      }
      if (!Claim(searches, $"{local}>{target}", minInterval)) {
        return;
      }
      ThreadPool.QueueUserWorkItem(_ => DoSearch(local, target));
    }

    private static bool Claim<T>(ConcurrentDictionary<T, DateTime> dict,
      T key, TimeSpan interval)
    {
      var now = DateTime.UtcNow;
      var claimed = false;
      dict.AddOrUpdate(
        key,
        k => {
          claimed = true;
          return now + interval;
        },
        (k, next) => {
          if (next > now) {
            return next;
          }
          claimed = true;
          return now + interval;
        });
      return claimed;
    }

    private void DoSearch(IPAddress local, IPEndPoint target)
    {
      var multicast = target.Equals(ssdpEndp);
      var message = new StringBuilder();
      message.Append("M-SEARCH * HTTP/1.1\r\n");
      message.AppendFormat("HOST: {0}\r\n", target);
      message.Append("MAN: \"ssdp:discover\"\r\n");
      if (multicast) {
        message.AppendFormat("MX: {0}\r\n", SEARCH_MX);
      }
      message.AppendFormat("ST: {0}\r\n", RENDERER_TYPE);
      message.AppendFormat("USER-AGENT: {0}\r\n", HttpServer.Signature);
      message.Append("\r\n");
      var bytes = Encoding.ASCII.GetBytes(message.ToString());

      try {
        // Search from an ephemeral port, as the responses are unicast back to
        // the sender and the main SSDP socket is bound to 1900.
        using (var udp = new UdpClient(new IPEndPoint(local, 0))) {
          udp.Client.SetSocketOption(
            SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 4);
          udp.Client.ReceiveTimeout = 500;
          udp.Send(bytes, bytes.Length, target);
          DebugFormat("{0} - Sent renderer M-SEARCH to {1}", local, target);

          var deadline = DateTime.UtcNow.AddSeconds(SEARCH_MX + 1);
          while (DateTime.UtcNow < deadline) {
            byte[] data;
            var from = new IPEndPoint(IPAddress.Any, 0);
            try {
              data = udp.Receive(ref from);
            }
            catch (SocketException ex)
              when (ex.SocketErrorCode == SocketError.TimedOut) {
              continue;
            }
            string proto;
            var headers = ParseSsdp(data, out proto);
            if (!proto.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase)) {
              continue;
            }
            string st, location, server;
            headers.TryGetValue("ST", out st);
            headers.TryGetValue("LOCATION", out location);
            headers.TryGetValue("SERVER", out server);
            Announce(from.Address, st, location, server);
          }
        }
      }
      catch (Exception ex) {
        Debug($"{local} - Renderer search to {target} failed", ex);
      }
    }

    private void Fetch(IPAddress address, Uri location, string server)
    {
      try {
        XmlDocument description;
        using (var response = CreateRequest(location).GetResponse())
        using (var stream = response.GetResponseStream()) {
          description = LoadXml(stream);
        }

        var device =
          description.SelectSingleNode(
            $"//*[local-name() = 'device'][starts-with(normalize-space(*[local-name() = 'deviceType']), '{RENDERER_PREFIX}')]") ??
          description.SelectSingleNode("//*[local-name() = 'device']");
        if (device == null) {
          throw new InvalidDataException("No device in description");
        }

        IList<DlnaProtocolInfo> sink = null;
        var service = device.SelectSingleNode(
          $"*[local-name() = 'serviceList']/*[local-name() = 'service'][starts-with(normalize-space(*[local-name() = 'serviceType']), '{CONNECTION_MANAGER_PREFIX}')]");
        if (service == null) {
          WarnFormat("{0} - Renderer has no ConnectionManager", location);
        }
        else {
          var baseUri = location;
          var urlBase = ChildText(description.DocumentElement, "URLBase");
          if (!string.IsNullOrEmpty(urlBase)) {
            Uri.TryCreate(urlBase, UriKind.Absolute, out baseUri);
          }
          var control = new Uri(
            baseUri ?? location, ChildText(service, "controlURL"));
          sink = GetSinkProtocolInfo(
            control, ChildText(service, "serviceType"));
        }

        var profile = new ClientProfile(
          address, location, server,
          ChildText(device, "deviceType"),
          ChildText(device, "friendlyName"),
          ChildText(device, "manufacturer"),
          ChildText(device, "modelName"),
          sink);
        profiles[address] = profile;
        InfoFormat(
          "Renderer {0} advertises {1} sink protocols",
          profile, profile.SinkProtocols.Count);
        DebugFormat(
          "{0} - Sink protocols:\n{1}",
          address, string.Join("\n", profile.SinkProtocols));
      }
      catch (Exception ex) {
        // Allow a retry sooner than the normal refresh
        fetches[location] = DateTime.UtcNow + retryInterval;
        Warn($"{address} - Failed to query renderer at {location}", ex);
      }
    }

    private IList<DlnaProtocolInfo> GetSinkProtocolInfo(Uri control,
      string serviceType)
    {
      var envelope =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
        $"<s:Body><u:GetProtocolInfo xmlns:u=\"{SecurityElement.Escape(serviceType)}\"/></s:Body>" +
        "</s:Envelope>";
      var body = Encoding.UTF8.GetBytes(envelope);

      var request = CreateRequest(control);
      request.Method = "POST";
      request.ContentType = "text/xml; charset=\"utf-8\"";
      request.Headers.Add("SOAPACTION", $"\"{serviceType}#GetProtocolInfo\"");
      request.ContentLength = body.Length;
      using (var stream = request.GetRequestStream()) {
        stream.Write(body, 0, body.Length);
      }

      XmlDocument result;
      using (var response = request.GetResponse())
      using (var stream = response.GetResponseStream()) {
        result = LoadXml(stream);
      }
      var sink = result.SelectSingleNode("//*[local-name() = 'Sink']");
      return DlnaProtocolInfo.ParseList(sink?.InnerText);
    }
  }
}
