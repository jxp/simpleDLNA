using System;
using System.Net;

namespace NMaier.SimpleDlna.Server
{
  /// <summary>
  ///   A software client, identified by its User-Agent header.
  /// </summary>
  public sealed class ClientInfo
  {
    public ClientInfo(string userAgent)
    {
      if (string.IsNullOrEmpty(userAgent)) {
        throw new ArgumentNullException(nameof(userAgent));
      }
      UserAgent = userAgent;
      Description = userAgent;
    }

    private string description;

    /// <summary>
    ///   Defaults to (and reverts to, when cleared) the User-Agent
    /// </summary>
    public string Description
    {
      get { return description; }
      set {
        description = string.IsNullOrWhiteSpace(value)
          ? UserAgent
          : value.Trim();
      }
    }

    /// <summary>
    ///   Address of the most recent request from this client
    /// </summary>
    public IPAddress LastAddress { get; internal set; }

    public DateTime LastSeen { get; internal set; }

    public bool TranscodeAudio { get; set; }

    public string UserAgent { get; }

    public override string ToString()
    {
      return Description == UserAgent
        ? Description
        : $"{Description} [{UserAgent}]";
    }
  }
}
