using NMaier.SimpleDlna.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NMaier.SimpleDlna.Server
{

  /// <summary>
  ///   A software client, identified by the User-Agent headers it sends.
  ///   Many devices use more than one, e.g. one for browsing and another for
  ///   playback.
  /// </summary>
  public sealed class ClientInfo
  {
    private string description;

    // Replaced, never modified, so it can be read without locking
    private string[] userAgents;

    public ClientInfo(string userAgent)
    {
      if (string.IsNullOrWhiteSpace(userAgent)) {
        throw new ArgumentNullException(nameof(userAgent));
      }
      userAgents = new[] {userAgent.Trim()};
    }

    /// <summary>
    ///   The User-Agents other than the main one
    /// </summary>
    public IList<string> AdditionalUserAgents
    {
      get {
        var main = UserAgent;
        return userAgents.Where(u => u != main).ToList().AsReadOnly();
      }
    }

    /// <summary>
    ///   Defaults to (and reverts to, when cleared) the main User-Agent
    /// </summary>
    public string Description
    {
      get { return description ?? UserAgent; }
      set {
        description = string.IsNullOrWhiteSpace(value) ||
                      value.Trim() == UserAgent
          ? null
          : value.Trim();
      }
    }

    /// <summary>
    ///   Address of the most recent request from this client
    /// </summary>
    public IPAddress LastAddress { get; internal set; }

    public DateTime LastSeen { get; internal set; }

    public TranscodeMode? TranscodeAudio { get; set; }

    public string TranscodeDescription {
      get
      {
        if (!TranscodeAudio.HasValue)
        {
          return "";
        }
        return TranscodeAudio.ToString();
      }
      set
      {
        if (string.IsNullOrEmpty(value))
        {
          TranscodeAudio = null;
        }
        else
        {
          TranscodeAudio = (TranscodeMode)Enum.Parse(typeof(TranscodeMode), value);
        }
      }
    }


    /// <summary>
    ///   The main User-Agent: the most specific (longest) one. Shorter ones,
    ///   such as "Roku/4400X", are often shared by several devices.
    /// </summary>
    public string UserAgent
    {
      get {
        var uas = userAgents;
        return uas.Aggregate((best, u) => u.Length > best.Length ? u : best);
      }
    }

    public IList<string> UserAgents => Array.AsReadOnly(userAgents);

    internal void AddUserAgent(string userAgent)
    {
      if (string.IsNullOrWhiteSpace(userAgent) || HasUserAgent(userAgent)) {
        return;
      }
      userAgents = userAgents.Concat(new[] {userAgent.Trim()}).ToArray();
    }

    internal bool HasUserAgent(string userAgent)
    {
      return userAgents.Contains(userAgent, StringComparer.OrdinalIgnoreCase);
    }

    internal bool IsMainUserAgent(string userAgent)
    {
      return UserAgent.Equals(userAgent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///   Takes over the User-Agents of a client that turned out to be this one
    /// </summary>
    internal void Merge(ClientInfo other)
    {
      foreach (var ua in other.userAgents) {
        AddUserAgent(ua);
      }
      if (description == null) {
        description = other.description;
      }
      if (other.TranscodeAudio.HasValue && other.TranscodeAudio.Value == TranscodeMode.Advanced)
      {
        TranscodeAudio = other.TranscodeAudio.Value;
      }
      if (!TranscodeAudio.HasValue && other.TranscodeAudio.HasValue && other.TranscodeAudio.Value == TranscodeMode.Yes)
      {
        TranscodeAudio = other.TranscodeAudio.Value;
      }

      if (other.LastSeen > LastSeen) {
        LastSeen = other.LastSeen;
        LastAddress = other.LastAddress;
      }
    }

    public override string ToString()
    {
      return description == null
        ? UserAgent
        : $"{description} [{UserAgent}]";
    }
  }
}
