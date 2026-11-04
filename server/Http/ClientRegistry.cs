using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NMaier.SimpleDlna.Utilities;

namespace NMaier.SimpleDlna.Server
{
  /// <summary>
  ///   Records the clients that have made requests. A client is identified by
  ///   its User-Agents together with the address it uses them from.
  ///   Shared by all servers so it survives a server reload.
  /// </summary>
  public sealed class ClientRegistry : Logging
  {
    // A new User-Agent from an address that made a request this recently is
    // taken to be the same device
    private static readonly TimeSpan groupWindow = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan reconnectAfter = TimeSpan.FromHours(4);

    public static readonly ClientRegistry Instance = new ClientRegistry();

    private readonly List<ClientInfo> clients = new List<ClientInfo>();

    private ClientRegistry()
    {
    }

    public ICollection<ClientInfo> Clients
    {
      get {
        lock (clients) {
          return clients.ToArray();
        }
      }
    }

    private static ClientInfo MostRecent(IEnumerable<ClientInfo> candidates)
    {
      return candidates.OrderByDescending(c => c.LastSeen).FirstOrDefault();
    }

    public bool Remove(ClientInfo client)
    {
      lock (clients) {
        return clients.Remove(client);
      }
    }

    /// <summary>
    ///   Re-creates a previously saved client. A saved client with the same
    ///   main User-Agent as an existing one is merged into it.
    /// </summary>
    public ClientInfo Restore(IEnumerable<string> userAgents,
      string description, bool transcodeAudio, DateTime lastSeen,
      IPAddress lastAddress)
    {
      var uas = (userAgents ?? Enumerable.Empty<string>())
        .Where(u => !string.IsNullOrWhiteSpace(u))
        .ToList();
      if (uas.Count == 0) {
        return null;
      }
      var client = new ClientInfo(uas[0]);
      foreach (var ua in uas.Skip(1)) {
        client.AddUserAgent(ua);
      }
      client.Description = description;
      client.TranscodeAudio = transcodeAudio;
      client.LastSeen = lastSeen;
      client.LastAddress = lastAddress;

      lock (clients) {
        var existing = clients.FirstOrDefault(
          c => c.IsMainUserAgent(client.UserAgent));
        if (existing != null) {
          existing.Merge(client);
          return existing;
        }
        clients.Add(client);
      }
      return client;
    }

    /// <summary>
    ///   Records a request. Returns null when the request has no User-Agent.
    /// </summary>
    internal ClientInfo Seen(IHeaders headers, IPEndPoint endpoint)
    {
      string userAgent;
      if (!headers.TryGetValue("User-Agent", out userAgent) ||
          string.IsNullOrWhiteSpace(userAgent)) {
        return null;
      }
      userAgent = userAgent.Trim();
      var address = endpoint?.Address;
      var now = DateTime.Now;

      ClientInfo client;
      bool grouped, connected;
      lock (clients) {
        client = Match(userAgent, address, now, out grouped);
        connected = client.LastSeen == DateTime.MinValue ||
                    now - client.LastSeen > reconnectAfter;
        client.LastSeen = now;
        client.LastAddress = address;
      }
      if (grouped) {
        InfoFormat(
          "{0} - Client {1} also uses User-Agent {2}",
          endpoint, client, userAgent);
      }
      if (connected) {
        InfoFormat("{0} - Client connected: {1}", endpoint, client);
      }
      return client;
    }

    // Must hold the lock on clients
    private ClientInfo Match(string userAgent, IPAddress address,
      DateTime now, out bool grouped)
    {
      grouped = false;

      // Seen before, from this address
      var client = MostRecent(clients.Where(
        c => Equals(c.LastAddress, address) && c.HasUserAgent(userAgent)));
      if (client != null) {
        return client;
      }

      // A new User-Agent from an address that is busy right now: the same
      // device using another of its User-Agents
      var active = address == null
        ? null
        : MostRecent(clients.Where(
          c => Equals(c.LastAddress, address) &&
               now - c.LastSeen <= groupWindow));
      if (active != null) {
        grouped = true;
        // If this is another client's main User-Agent then the active client
        // was started from a shared User-Agent, so it is really that client
        var owner = MostRecent(clients.Where(
          c => c != active && c.IsMainUserAgent(userAgent)));
        if (owner != null) {
          owner.Merge(active);
          clients.Remove(active);
          return owner;
        }
        active.AddUserAgent(userAgent);
        return active;
      }

      // From a new address, only trust a client's main User-Agent. Its
      // other ones may be shared by several devices (e.g. "Roku/4400X").
      client = MostRecent(clients.Where(c => c.IsMainUserAgent(userAgent)));
      if (client != null) {
        return client;
      }

      client = new ClientInfo(userAgent);
      clients.Add(client);
      return client;
    }
  }
}
