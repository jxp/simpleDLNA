namespace NMaier.SimpleDlna.Server
{
  public enum DlnaMime
  {
    AudioAAC,
    AudioFLAC,
    AudioMP2,
    AudioMP3,
    AudioRAW,
    AudioVORBIS,
    ImageGIF,
    ImageJPEG,
    ImagePNG,
    SubtitleSRT,
    // The second subtitle mimme type is not used in code, but is advertised as available to DLNA clients
    SubtitleSRT2,
    Video3GPP,
    VideoAVC,
    VideoAVI,
    VideoFLV,
    VideoMKV,
    VideoMPEG,
    VideoOGV,
    VideoWMV
  }
}
