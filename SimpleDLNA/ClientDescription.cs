using NMaier.SimpleDlna.Utilities;
using System;

namespace NMaier.SimpleDlna.GUI
{
  /// <summary>
  ///   A known client, as stored in the user settings
  /// </summary>
  [Serializable]
  public sealed class ClientDescription
  {
    public ClientDescription()
    {
      AdditionalUserAgents = new string[0];
    }

    public string[] AdditionalUserAgents { get; set; }

    public string Description { get; set; }

    public string LastAddress { get; set; }

    public DateTime LastSeen { get; set; }

    public TranscodeMode? TranscodeAudio { get; set; }

    public string UserAgent { get; set; }
  }
}
