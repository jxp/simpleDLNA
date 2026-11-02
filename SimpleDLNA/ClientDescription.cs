using System;

namespace NMaier.SimpleDlna.GUI
{
  /// <summary>
  ///   A known client, as stored in the user settings
  /// </summary>
  [Serializable]
  public sealed class ClientDescription
  {
    public string Description { get; set; }

    public DateTime LastSeen { get; set; }

    public bool TranscodeAudio { get; set; }

    public string UserAgent { get; set; }
  }
}
