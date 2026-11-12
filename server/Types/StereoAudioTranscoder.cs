using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
    ///   {CacheDirectory}\FacebookVideo1_AC3_572AD49E.mkv
    ///   The hash of the full path keeps same-named files apart.
    /// </summary>
    public static string GetOutputPath(FileInfo source, string encoder)
    {
      byte[] hash;
      using (var md5 = MD5.Create()) {
        hash = md5.ComputeHash(
          Encoding.UTF8.GetBytes(source.FullName.ToUpperInvariant()));
      }
      var suffix = BitConverter.ToString(hash, 0, 4).Replace("-", string.Empty);
      return Path.Combine(
        CacheDirectory,
        $"{Path.GetFileNameWithoutExtension(source.Name)}_{encoder}_{suffix}{source.Extension}");
    }

    /// <summary>
    ///   How long a request waits for a new copy before deciding whether to
    ///   keep waiting or to stream it while it's being made.
    /// </summary>
    public static TimeSpan StreamDelay { get; set; } = TimeSpan.FromSeconds(25);

    /// <summary>
    ///   Keep waiting for the complete copy (with seeking and duration) when
    ///   it's expected within this long of the request. Keep it under the
    ///   client's own timeout (about 2 minutes on the TVs tested).
    /// </summary>
    public static TimeSpan MaxWaitForFullFile { get; set; } =
      TimeSpan.FromSeconds(90);

    // Copy path -> the transcode making it, shared by all requests for it
    private static readonly ConcurrentDictionary<string, Lazy<Job>> jobs =
      new ConcurrentDictionary<string, Lazy<Job>>(
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///   The stereo copy of a video whose first audio stream has more than
    ///   two channels, starting it if needed. Waits for a new copy as set by
    ///   StreamDelay and MaxWaitForFullFile; if it's still not done (and the
    ///   container allows), returns it in progress to stream as it's made.
    ///   Returns null when no copy is needed or it can't be made.
    /// </summary>
    public static StereoCopy GetStereoCopy(IMediaResource item, TranscodeMode mode)
    {
      var requested = DateTime.Now;
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

      var encoder = mode == TranscodeMode.Yes ? "aac" : "ac3";
      var output = new FileInfo(GetOutputPath(source, encoder));

      lock (locks.GetOrAdd(output.FullName, k => new object())) {
        output.Refresh();
        if (!jobs.ContainsKey(output.FullName) &&
            output.Exists && output.Length > 0 &&
            output.LastWriteTimeUtc >= source.LastWriteTimeUtc) {
          wanted[version] = true;
          served[output.FullName] = DateTime.Now;
          return new StereoCopy(output);
        }
        // Check if the advanced mode can fallback to the simple mode
        if (mode == TranscodeMode.Advanced)
        {
          var outputFallback = new FileInfo(GetOutputPath(source, "aac"));
          if (!jobs.ContainsKey(outputFallback.FullName) &&
              outputFallback.Exists && outputFallback.Length > 0 &&
              outputFallback.LastWriteTimeUtc >= source.LastWriteTimeUtc)
          {
            wanted[version] = true;
            served[outputFallback.FullName] = DateTime.Now;
            return new StereoCopy(outputFallback);
          }

        }

        if (!wanted.TryGetValue(version, out want)) {
          try {
            var channels = FFmpeg.GetFirstAudioChannels(source);
            want = channels > 2;
            log.DebugFormat(
              "{0}: first audio stream has {1} channels", source.FullName,
              channels);
          }
          catch (Exception ex) {
            log.Warn($"Failed to read audio channels of {source.FullName}", ex);
            want = false;
          }
          wanted[version] = want;
        }
        if (!want) {
          return null;
        }
      }

      var job = jobs.GetOrAdd(
        output.FullName,
        k => new Lazy<Job>(() => StartJob(source, output, version, mode))).Value;
      if (job.Task.Wait(StreamDelay)) {
        return Finished(job);
      }
      if (!FFmpeg.IsStreamable(output.Extension)) {
        // A partly written MP4 can't be played, so wait for all of it
        job.Task.Wait();
        return Finished(job);
      }

      // Worth waiting for the complete file if it will be done in time
      for (;;) {
        var waited = DateTime.Now - requested;
        var remaining = job.EstimateRemaining();
        if (remaining == null || waited + remaining.Value > MaxWaitForFullFile) {
          log.InfoFormat(
            "{0} needs about {1} more; streaming it while it's made (no seeking)",
            output.Name,
            remaining == null ? "unknown" : $"{remaining.Value.TotalSeconds:0}s");
          break;
        }
        log.DebugFormat(
          "{0} needs about {1:0}s more; waiting for all of it",
          output.Name, remaining.Value.TotalSeconds);
        if (job.Task.Wait(TimeSpan.FromSeconds(1))) {
          return Finished(job);
        }
      }
      return job.Task.IsCompleted
        ? Finished(job)
        : new StereoCopy(job.PartialPath, job.Task);
    }

    private static StereoCopy Finished(Job job)
    {
      var file = job.Task.Result;
      if (file == null) {
        return null;
      }
      served[file.FullName] = DateTime.Now;
      return new StereoCopy(file);
    }

    private static Job StartJob(FileInfo source, FileInfo output,
      string version, TranscodeMode audioMode)
    {
      var job = new Job(source, FFmpeg.GetPartialPath(output));
      job.Task = Task.Run(() => {
        try {
          Directory.CreateDirectory(CacheDirectory);
          log.InfoFormat(
            "Transcoding audio to stereo: {0} -> {1}", source.FullName,
            output.FullName);
          FFmpeg.RemuxWithStereoAudio(source, output, audioMode, job.Progress);
          output.Refresh();
          DateTime retryAt;
          failed.TryRemove(version, out retryAt);
          log.InfoFormat(
            "Transcoded {0} in {1:0.0}s", output.Name,
            (DateTime.Now - job.Started).TotalSeconds);
          return output;
        }
        catch (Exception ex) {
          var retryAt = DateTime.Now + retryFailedAfter;
          failed[version] = retryAt;
          log.Error(
            $"Failed to transcode audio of {source.FullName}; serving the original until {retryAt:T}",
            ex);
          return null;
        }
        finally {
          Lazy<Job> done;
          jobs.TryRemove(output.FullName, out done);
        }
      });
      return job;
    }

    private sealed class Job
    {
      // Source duration in seconds, 0 if unknown
      private readonly double duration;

      private readonly long expectedSize;

      // How far through the source ffmpeg is, in ticks
      private long done;

      public Job(FileInfo source, string partialPath)
      {
        PartialPath = partialPath;
        // Only a fallback: how much smaller the copy is depends on how much
        // of the file was multichannel audio
        expectedSize = source.Length;
        try {
          duration = FFmpeg.GetFileDuration(source);
        }
        catch (Exception) {
          duration = 0;
        }
      }

      public string PartialPath { get; }

      public DateTime Started { get; } = DateTime.Now;

      public Task<FileInfo> Task { get; set; }

      public void Progress(TimeSpan position)
      {
        Interlocked.Exchange(ref done, position.Ticks);
      }

      /// <summary>
      ///   From how far through the source ffmpeg is, or failing that how
      ///   fast the copy is growing; null until either is known
      /// </summary>
      public TimeSpan? EstimateRemaining()
      {
        var elapsed = (DateTime.Now - Started).TotalSeconds;
        var position = TimeSpan.FromTicks(Interlocked.Read(ref done))
          .TotalSeconds;
        if (duration > 0 && position > 0 && elapsed > 0) {
          return TimeSpan.FromSeconds(
            elapsed * Math.Max(0, duration - position) / position);
        }
        try {
          var written = new FileInfo(PartialPath).Length;
          if (written <= 0 || elapsed <= 0) {
            return null;
          }
          return TimeSpan.FromSeconds(
            Math.Max(0, expectedSize - written) / (written / elapsed));
        }
        catch (Exception) {
          return null;
        }
      }
    }
  }

  /// <summary>
  ///   A stereo copy that is either complete (File) or still being written
  ///   (PartialPath, finished when Completion is).
  /// </summary>
  public sealed class StereoCopy
  {
    internal StereoCopy(FileInfo file)
    {
      File = file;
    }

    internal StereoCopy(string partialPath, Task<FileInfo> completion)
    {
      PartialPath = partialPath;
      Completion = completion;
    }

    public Task<FileInfo> Completion { get; }

    public FileInfo File { get; }

    public bool IsComplete => File != null;

    public string PartialPath { get; }
  }
}
