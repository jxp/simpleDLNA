using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using NMaier.SimpleDlna.Utilities;

namespace NMaier.SimpleDlna.Server
{
  /// <summary>
  ///   Records the clients that have made requests, keyed by User-Agent.
  ///   Shared by all servers so it survives a server reload.
  /// </summary>
  public sealed class ClientRegistry : Logging
  {
    private static readonly TimeSpan reconnectAfter = TimeSpan.FromHours(4);

    public static readonly ClientRegistry Instance = new ClientRegistry();

    private readonly ConcurrentDictionary<string, ClientInfo> clients =
      new ConcurrentDictionary<string, ClientInfo>(
        StringComparer.OrdinalIgnoreCase);

    private ClientRegistry()
    {
    }

    public ICollection<ClientInfo> Clients => clients.Values;

    public ClientInfo Get(string userAgent)
    {
      ClientInfo rv;
      return userAgent != null && clients.TryGetValue(userAgent, out rv)
        ? rv
        : null;
    }

    public bool Remove(string userAgent)
    {
      ClientInfo removed;
      return userAgent != null && clients.TryRemove(userAgent, out removed);
    }

    /// <summary>
    ///   Re-creates a previously saved client. An existing entry is updated,
    ///   keeping the most recent LastSeen.
    /// </summary>
    public ClientInfo Restore(string userAgent, string description,
      bool transcodeAudio, DateTime lastSeen)
    {
      if (string.IsNullOrWhiteSpace(userAgent)) {
        return null;
      }
      var client = clients.GetOrAdd(
        userAgent.Trim(), ua => new ClientInfo(ua));
      lock (client) {
        client.Description = description;
        client.TranscodeAudio = transcodeAudio;
        if (lastSeen > client.LastSeen) {
          client.LastSeen = lastSeen;
        }
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
      var client = clients.GetOrAdd(userAgent, ua => new ClientInfo(ua));

      var now = DateTime.Now;
      bool connected;
      lock (client) {
        connected = client.LastSeen == DateTime.MinValue ||
                    now - client.LastSeen > reconnectAfter;
        client.LastSeen = now;
        client.LastAddress = endpoint?.Address;
      }
      if (connected) {
        InfoFormat("{0} - Client connected: {1}", endpoint, client);
      }
      return client;
    }
  }
}
