using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using log4net;
using NMaier.SimpleDlna.Utilities;

namespace NMaier.SimpleDlna.Server
{
  /// <summary>
  ///   Makes copies of videos with stereo AAC audio, for clients that can't
  ///   play multichannel audio (ClientInfo.TranscodeAudio). Copies are kept
  ///   in the cache directory and reused until the source changes.
  /// </summary>
  public static class StereoAudioTranscoder
  {
    private static readonly ILog log =
      LogManager.GetLogger(typeof(StereoAudioTranscoder));

    private static readonly ConcurrentDictionary<string, object> locks =
      new ConcurrentDictionary<string, object>(
        StringComparer.OrdinalIgnoreCase);

    // Source version -> whether a stereo copy should be served. Avoids
    // re-probing on every (range) request, and retrying failed transcodes.
    private static readonly ConcurrentDictionary<string, bool> wanted =
      new ConcurrentDictionary<string, bool>(
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///   Where the stereo copies go. Transcoding is off while this is unset.
    /// </summary>
    public static string CacheDirectory { get; set; }

    /// <summary>
    ///   e.g. C:\Media\Videos\2026\FacebookVideo1.mkv ->
    ///   {CacheDirectory}\FacebookVideo1_572AD49E.mkv
    ///   The hash of the full path keeps same-named files apart.
    /// </summary>
    public static string GetOutputPath(FileInfo source)
    {
      byte[] hash;
      using (var md5 = MD5.Create()) {
        hash = md5.ComputeHash(
          Encoding.UTF8.GetBytes(source.FullName.ToUpperInvariant()));
      }
      var suffix = BitConverter.ToString(hash, 0, 4).Replace("-", string.Empty);
      return Path.Combine(
        CacheDirectory,
        $"{Path.GetFileNameWithoutExtension(source.Name)}_{suffix}{source.Extension}");
    }

    /// <summary>
    ///   The stereo copy of a video whose first audio stream has more than
    ///   two channels, creating it first if needed (which blocks until done).
    ///   Returns null when no copy is needed or it can't be made.
    /// </summary>
    public static FileInfo GetStereoFile(IMediaResource item)
    {
      if (item?.MediaType != DlnaMediaTypes.Video ||
          string.IsNullOrWhiteSpace(CacheDirectory) ||
          FFmpeg.FFmpegExecutable == null) {
        return null;
      }
      var source = new FileInfo(item.Path);
      if (!source.Exists) {
        return null;
      }
      var version =
        $"{source.FullName}|{source.Length}|{source.LastWriteTimeUtc.Ticks}";
      bool want;
      if (wanted.TryGetValue(version, out want) && !want) {
        return null;
      }

      var output = new FileInfo(GetOutputPath(source));
      lock (locks.GetOrAdd(output.FullName, k => new object())) {
        output.Refresh();
        if (output.Exists && output.Length > 0 &&
            output.LastWriteTimeUtc >= source.LastWriteTimeUtc) {
          wanted[version] = true;
          return output;
        }

        try {
          if (!wanted.TryGetValue(version, out want)) {
            var channels = FFmpeg.GetFirstAudioChannels(source);
            want = channels > 2;
            wanted[version] = want;
            log.DebugFormat(
              "{0}: first audio stream has {1} channels", source.FullName,
              channels);
          }
          if (!want) {
            return null;
          }

          Directory.CreateDirectory(CacheDirectory);
          log.InfoFormat(
            "Transcoding audio to stereo: {0} -> {1}", source.FullName,
            output.FullName);
          var started = DateTime.Now;
          FFmpeg.RemuxWithStereoAudio(source, output);
          output.Refresh();
          log.InfoFormat(
            "Transcoded {0} in {1:0.0}s", output.Name,
            (DateTime.Now - started).TotalSeconds);
          return output;
        }
        catch (Exception ex) {
          // Serve the original from now on, until the source changes
          wanted[version] = false;
          log.Error($"Failed to transcode audio of {source.FullName}", ex);
          return null;
        }
      }
    }
  }
}
