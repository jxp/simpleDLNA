using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using NMaier.SimpleDlna.Utilities;

namespace NMaier.SimpleDlna.Server
{
  /// <summary>
  ///   Makes copies of videos with stereo AC3 audio, for clients that can't
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

    private static readonly TimeSpan retryFailedAfter =
      TimeSpan.FromMinutes(10);

    // Source version -> whether a stereo copy should be served. Avoids
    // re-probing on every (range) request.
    private static readonly ConcurrentDictionary<string, bool> wanted =
      new ConcurrentDictionary<string, bool>(
        StringComparer.OrdinalIgnoreCase);

    // Source version -> when a failed transcode may be tried again. Until
    // then the original is served, rather than every request waiting on
    // another attempt.
    private static readonly ConcurrentDictionary<string, DateTime> failed =
      new ConcurrentDictionary<string, DateTime>(
        StringComparer.OrdinalIgnoreCase);

    // Copies not made or served for this long are deleted
    private static readonly TimeSpan maxAge = TimeSpan.FromDays(7);

    private static readonly TimeSpan cleanupInterval = TimeSpan.FromHours(6);

    // Name_1A2B3C4D.mkv, or Name_1A2B3C4D.mkv.partial left by a failed run
    private static readonly Regex copyName = new Regex(
      @"_[0-9A-F]{8}\.([^.]+)(\.partial)?$",
      RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Copy path -> when it was last served (since this process started)
    private static readonly ConcurrentDictionary<string, DateTime> served =
      new ConcurrentDictionary<string, DateTime>(
        StringComparer.OrdinalIgnoreCase);

    private static Timer cleanupTimer;

    private static string cacheDirectory;

    /// <summary>
    ///   Where the stereo copies go. Transcoding is off while this is unset.
    ///   Setting it starts the periodic clean up of old copies.
    /// </summary>
    public static string CacheDirectory
    {
      get { return cacheDirectory; }
      set {
        cacheDirectory = value;
        if (cleanupTimer == null && !string.IsNullOrWhiteSpace(value)) {
          cleanupTimer = new Timer(
            _ => DeleteOldCopies(), null, TimeSpan.FromMinutes(1),
            cleanupInterval);
        }
      }
    }

    /// <summary>
    ///   Deletes copies that haven't been made or served for over a week.
    ///   Only files named like copies (see GetOutputPath) are touched.
    /// </summary>
    public static void DeleteOldCopies()
    {
      var dir = CacheDirectory;
      if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) {
        return;
      }
      var cutoff = DateTime.Now - maxAge;
      var count = 0;
      long bytes = 0;
      try {
        foreach (var file in new DirectoryInfo(dir).EnumerateFiles()) {
          var match = copyName.Match(file.Name);
          DlnaMediaTypes type;
          if (!match.Success ||
              !DlnaMaps.Ext2Media.TryGetValue(
                match.Groups[1].Value.ToUpperInvariant(), out type) ||
              type != DlnaMediaTypes.Video) {
            continue;
          }
          var lastUsed = file.LastWriteTime;
          DateTime lastServed;
          if (served.TryGetValue(file.FullName, out lastServed) &&
              lastServed > lastUsed) {
            lastUsed = lastServed;
          }
          if (lastUsed > cutoff) {
            continue;
          }
          try {
            var length = file.Length;
            file.Delete();
            served.TryRemove(file.FullName, out lastServed);
            count++;
            bytes += length;
            log.DebugFormat(
              "Deleted old copy {0}, last used {1:g}", file.Name, lastUsed);
          }
          catch (Exception ex) {
            log.Warn($"Failed to delete old copy {file.FullName}", ex);
          }
        }
      }
      catch (Exception ex) {
        log.Warn($"Failed to clean up {dir}", ex);
      }
      if (count != 0) {
        log.InfoFormat(
          "Deleted {0} stereo copies unused for over {1} days ({2})", count,
          maxAge.TotalDays, bytes.FormatFileSize());
      }
    }

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
      DateTime retryAt;
      if (failed.TryGetValue(version, out retryAt) &&
          DateTime.Now < retryAt) {
        return null;
      }

      var output = new FileInfo(GetOutputPath(source));
      lock (locks.GetOrAdd(output.FullName, k => new object())) {
        output.Refresh();
        if (output.Exists && output.Length > 0 &&
            output.LastWriteTimeUtc >= source.LastWriteTimeUtc) {
          wanted[version] = true;
          served[output.FullName] = DateTime.Now;
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
          failed.TryRemove(version, out retryAt);
          served[output.FullName] = DateTime.Now;
          log.InfoFormat(
            "Transcoded {0} in {1:0.0}s", output.Name,
            (DateTime.Now - started).TotalSeconds);
          return output;
        }
        catch (Exception ex) {
          retryAt = DateTime.Now + retryFailedAfter;
          failed[version] = retryAt;
          log.Error(
            $"Failed to transcode audio of {source.FullName}; serving the original until {retryAt:T}",
            ex);
          return null;
        }
      }
    }
  }
}
