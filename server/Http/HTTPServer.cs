using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Timers;
using log4net;
using NMaier.SimpleDlna.Server.Ssdp;
using NMaier.SimpleDlna.Utilities;

namespace NMaier.SimpleDlna.Server
{
  public sealed class HttpServer : Logging, IDisposable
  {
    public static readonly string Signature = GenerateServerSignature();

    private readonly ConcurrentDictionary<HttpClient, DateTime> clients =
      new ConcurrentDictionary<HttpClient, DateTime>();

    private readonly ConcurrentDictionary<Guid, List<Guid>> devicesForServers =
      new ConcurrentDictionary<Guid, List<Guid>>();

    private readonly TcpListener listener;

    private readonly ConcurrentDictionary<string, IPrefixHandler> prefixes =
      new ConcurrentDictionary<string, IPrefixHandler>();

    private readonly ConcurrentDictionary<Guid, MediaMount> servers =
      new ConcurrentDictionary<Guid, MediaMount>();

    private readonly SsdpHandler ssdpServer;

    public event EventHandler ReloadRequired;


    private readonly Timer timeouter = new Timer(10 * 1000);

    public HttpServer()
      : this(0)
    {
    }

    public static ConcurrentQueue<bool> KeepAwake { get; set; } 

    public HttpServer(int port)
    {
      prefixes.TryAdd(
        "/favicon.ico",
        new StaticHandler(
          new ResourceResponse(HttpCode.Ok, "image/icon", "favicon"))
        );
      prefixes.TryAdd(
        "/static/browse.css",
        new StaticHandler(
          new ResourceResponse(HttpCode.Ok, "text/css", "browse_css"))
        );
      RegisterHandler(new IconHandler());

      listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
      listener.Server.Ttl = 32;
      listener.Server.UseOnlyOverlappedIO = true;
      listener.Start();

      RealPort = ((IPEndPoint)listener.LocalEndpoint).Port;

      NoticeFormat(
        "Running HTTP Server: {0} on port {1}", Signature, RealPort);
      ssdpServer = new SsdpHandler();
      ssdpServer.RepeatedAddressError += SsdpServer_RepeatedAddressError;

      timeouter.Elapsed += TimeouterCallback;
      timeouter.Enabled = true;

      Accept();
    }

    private void SsdpServer_RepeatedAddressError(object sender, EventArgs e)
    {
      // Pass this event on to the hosting class
      EventHandler handler = ReloadRequired;
      if (handler != null)
      {
        handler(this, EventArgs.Empty);
      }

    }

    public Dictionary<string, string> MediaMounts
    {
      get {
        var rv = new Dictionary<string, string>();
        foreach (var m in servers) {
          rv[m.Value.Prefix] = m.Value.FriendlyName;
        }
        return rv;
      }
    }

    public int RealPort { get; }

    public void Dispose()
    {
      Debug("Disposing HTTP");
      timeouter.Enabled = false;
      foreach (var s in servers.Values.ToList()) {
        UnregisterMediaServer(s);
      }
      ssdpServer.RepeatedAddressError -= SsdpServer_RepeatedAddressError;
      ssdpServer.Dispose();
      timeouter.Dispose();
      listener.Stop();
      foreach (var c in clients.ToList()) {
        c.Key.Dispose();
      }
      clients.Clear();
    }

    public event EventHandler<HttpAuthorizationEventArgs> OnAuthorizeClient;

    private void Accept()
    {
      try {
        if (!listener.Server.IsBound) {
          return;
        }
        listener.BeginAcceptTcpClient(AcceptCallback, null);
      }
      catch (ObjectDisposedException) {
      }
      catch (Exception ex) {
        Fatal("Failed to accept", ex);
      }
    }

    private void AcceptCallback(IAsyncResult result)
    {
      try {
        var tcpclient = listener.EndAcceptTcpClient(result);
        var client = new HttpClient(this, tcpclient);
        try {
          clients.AddOrUpdate(client, DateTime.Now, (k, v) => DateTime.Now);
          DebugFormat("Accepted client {0}", client);
          client.Start();
        }
        catch (Exception) {
          client.Dispose();
          throw;
        }
      }
      catch (ObjectDisposedException) {
      }
      catch (Exception ex) {
        Error("Failed to accept a client", ex);
      }
      finally {
        Accept();
      }
    }

    private static string GenerateServerSignature()
    {
      var os = Environment.OSVersion;
      var pstring = os.Platform.ToString();
      switch (os.Platform) {
      case PlatformID.Win32NT:
      case PlatformID.Win32S:
      case PlatformID.Win32Windows:
        pstring = "WIN";
        break;
      default:
        try {
          pstring = Formatting.GetSystemName();
        }
        catch (Exception ex) {
          LogManager.GetLogger(typeof (HttpServer)).Debug("Failed to get uname", ex);
        }
        break;
      }
      var version = Assembly.GetExecutingAssembly().GetName().Version;
      var bitness = IntPtr.Size * 8;
      return
        $"{pstring}{bitness}/{os.Version.Major}.{os.Version.Minor} UPnP/1.0 DLNADOC/1.5 sdlna/{version.Major}.{version.Minor}";
    }

    private void TimeouterCallback(object sender, ElapsedEventArgs e)
    {
      foreach (var c in clients.ToList()) {
        if (c.Key.IsATimeout) {
          DebugFormat("Collected timeout client {0}", c);
          c.Key.Close();
        }
      }
    }

    internal bool AuthorizeClient(HttpClient client)
    {
      if (OnAuthorizeClient == null) {
        return true;
      }
      if (IPAddress.IsLoopback(client.RemoteEndpoint.Address)) {
        return true;
      }
      var e = new HttpAuthorizationEventArgs(
        client.Headers, client.RemoteEndpoint);
      OnAuthorizeClient(this, e);
      return !e.Cancel;
    }

    internal IPrefixHandler FindHandler(string prefix)
    {
      if (string.IsNullOrEmpty(prefix)) {
        throw new ArgumentNullException(nameof(prefix));
      }

      if (prefix == "/") {
        return new IndexHandler(this);
      }

      return (from s in prefixes.Keys
              where prefix.StartsWith(s, StringComparison.Ordinal)
              select prefixes[s]).FirstOrDefault();
    }

    internal void RegisterHandler(IPrefixHandler handler)
    {
      if (handler == null) {
        throw new ArgumentNullException(nameof(handler));
      }
      var prefix = handler.Prefix;
      if (!prefix.StartsWith("/", StringComparison.Ordinal)) {
        throw new ArgumentException("Invalid prefix; must start with /");
      }
      if (!prefix.EndsWith("/", StringComparison.Ordinal)) {
        throw new ArgumentException("Invalid prefix; must end with /");
      }
      if (FindHandler(prefix) != null) {
        throw new ArgumentException("Invalid prefix; already taken");
      }
      if (!prefixes.TryAdd(prefix, handler)) {
        throw new ArgumentException("Invalid preifx; already taken");
      }
      DebugFormat("Registered Handler for {0}", prefix);
    }

    internal void RemoveClient(HttpClient client)
    {
      DateTime ignored;
      clients.TryRemove(client, out ignored);
    }

    internal void UnregisterHandler(IPrefixHandler handler)
    {
      IPrefixHandler ignored;
      if (prefixes.TryRemove(handler.Prefix, out ignored)) {
        DebugFormat("Unregistered Handler for {0}", handler.Prefix);
      }
    }

    public void RegisterMediaServer(IMediaServer server)
    {
      if (server == null) {
        throw new ArgumentNullException(nameof(server));
      }
      var guid = server.UUID;
      if (servers.ContainsKey(guid)) {
        throw new ArgumentException("Attempting to register more than once");
      }

      var end = (IPEndPoint)listener.LocalEndpoint;
      var mount = new MediaMount(server);
      servers[guid] = mount;
      RegisterHandler(mount);

      foreach (var address in IP.ExternalIPAddresses) {
        DebugFormat("Registering device for {0}", address);

        // Setup initial byes based on server and machine
        var macBytes = HexadecimalStringToByteArray(IP.GetMAC(address).Replace(":", ""));
        var addressByte = address.GetAddressBytes()[0];
        var serverBytes = MD5Hash(server.FriendlyName);
        var guidBytes = macBytes.Concat(new byte[]{ addressByte }).Concat(serverBytes.Take(16 - 1 - macBytes.Length)).ToArray();

        var lguid = new Guid(guidBytes);
        var deviceGuid = lguid;
        var list = devicesForServers.GetOrAdd(guid, new List<Guid>());
        lock (list) {
          list.Add(deviceGuid);
        }
        mount.AddDeviceGuid(deviceGuid, address);
        var uri = new Uri($"http://{address}:{end.Port}{mount.DescriptorURI}");
        lock (list) {
          ssdpServer.RegisterNotification(deviceGuid, uri, address);
        }
        NoticeFormat("New mount at: {0}", uri);
      }
    }

    private static byte[] HexadecimalStringToByteArray(string input)
    {
      var outputLength = input.Length / 2;
      var output = new byte[outputLength];
      for (var i = 0; i < outputLength; i++)
        output[i] = Convert.ToByte(input.Substring(i * 2, 2), 16);
      return output;
    }

    private static byte[] MD5Hash(string input)
    {
      using (var hash = MD5.Create())
      {
        return hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
      }
    }

    public void UnregisterMediaServer(IMediaServer server)
    {
      if (server == null) {
        throw new ArgumentNullException(nameof(server));
      }
      MediaMount mount;
      if (!servers.TryGetValue(server.UUID, out mount)) {
        return;
      }

      List<Guid> list;
      if (devicesForServers.TryGetValue(server.UUID, out list)) {
        lock (list) {
          foreach (var deviceGuid in list) {
            ssdpServer.UnregisterNotification(deviceGuid);
          }
        }
        devicesForServers.TryRemove(server.UUID, out list);
      }

      UnregisterHandler(mount);

      MediaMount ignored;
      if (servers.TryRemove(server.UUID, out ignored)) {
        InfoFormat("Unregistered Media Server {0}", server.UUID);
      }
    }
  }
}
