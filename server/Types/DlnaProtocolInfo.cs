using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NMaier.SimpleDlna.Server
{
  /// <summary>
  ///   A single UPnP protocolInfo entry, e.g.
  ///   http-get:*:video/mp4:DLNA.ORG_PN=AVC_MP4_BL_CIF15_AAC_520;DLNA.ORG_OP=01
  /// </summary>
  public sealed class DlnaProtocolInfo
  {
    private const string PN_KEY = "DLNA.ORG_PN=";

    // Commas inside an entry are escaped as "\,"
    private static readonly Regex listSeparator =
      new Regex(@"(?<!\\),", RegexOptions.Compiled);

    private DlnaProtocolInfo(string protocol, string network,
      string contentFormat, string additionalInfo)
    {
      Protocol = protocol;
      Network = network;
      ContentFormat = contentFormat;
      AdditionalInfo = additionalInfo;
      ProfileName = additionalInfo
        .Split(';')
        .Select(p => p.Trim())
        .Where(p => p.StartsWith(PN_KEY, StringComparison.OrdinalIgnoreCase))
        .Select(p => p.Substring(PN_KEY.Length))
        .FirstOrDefault();
    }

    public string AdditionalInfo { get; }

    /// <summary>
    ///   The MIME type, or *
    /// </summary>
    public string ContentFormat { get; }

    public string Network { get; }

    /// <summary>
    ///   The DLNA.ORG_PN value, or null when the entry has none
    /// </summary>
    public string ProfileName { get; }

    public string Protocol { get; }

    public bool Matches(string mime, string profileName)
    {
      if (Protocol != "*" &&
          !Protocol.Equals("http-get", StringComparison.OrdinalIgnoreCase)) {
        return false;
      }
      if (!MimeMatches(mime)) {
        return false;
      }
      return string.IsNullOrEmpty(profileName) ||
             string.IsNullOrEmpty(ProfileName) ||
             ProfileName.Equals(profileName, StringComparison.OrdinalIgnoreCase);
    }

    private bool MimeMatches(string mime)
    {
      if (ContentFormat == "*" ||
          ContentFormat.Equals(mime, StringComparison.OrdinalIgnoreCase)) {
        return true;
      }
      // e.g. video/*
      if (ContentFormat.EndsWith("/*", StringComparison.Ordinal)) {
        var major = ContentFormat.Substring(0, ContentFormat.Length - 1);
        return mime.StartsWith(major, StringComparison.OrdinalIgnoreCase);
      }
      return false;
    }

    public static DlnaProtocolInfo Parse(string value)
    {
      if (string.IsNullOrWhiteSpace(value)) {
        return null;
      }
      var parts = value.Trim().Split(new[] {':'}, 4);
      if (parts.Length != 4) {
        return null;
      }
      return new DlnaProtocolInfo(
        parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
    }

    public static IList<DlnaProtocolInfo> ParseList(string value)
    {
      if (string.IsNullOrWhiteSpace(value)) {
        return new DlnaProtocolInfo[0];
      }
      return listSeparator.Split(value)
        .Select(Parse)
        .Where(p => p != null)
        .ToList();
    }

    public override string ToString()
    {
      return $"{Protocol}:{Network}:{ContentFormat}:{AdditionalInfo}";
    }
  }
}
