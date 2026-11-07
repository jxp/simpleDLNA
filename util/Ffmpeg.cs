using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;

namespace NMaier.SimpleDlna.Utilities
{
  using InfoCache = LeastRecentlyUsedDictionary<FileInfo, IDictionary<string, string>>;

  public static class FFmpeg
  {
    private static readonly DirectoryInfo[] specialLocations =
    {
      GetFFMpegFolder(Environment.SpecialFolder.CommonProgramFiles),
      GetFFMpegFolder(Environment.SpecialFolder.CommonProgramFilesX86),
      GetFFMpegFolder(Environment.SpecialFolder.ProgramFiles),
      GetFFMpegFolder(Environment.SpecialFolder.ProgramFilesX86),
      GetFFMpegFolder(Environment.SpecialFolder.UserProfile),
      new DirectoryInfo(Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile))
    };

    private static readonly InfoCache infoCache = new InfoCache(500);

    private static readonly Regex regAssStrip =
      new Regex(@"^,+", RegexOptions.Compiled);

    private static readonly Regex regDuration = new Regex(
      @"Duration: ([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]+))?",
      RegexOptions.Compiled);

    private static readonly Regex regDimensions = new Regex(
      @"Video: .+ ([0-9]{2,})x([0-9]{2,}) ", RegexOptions.Compiled);

    // e.g. "Stream #0:1(eng): Audio: ac3, 48000 Hz, 5.1(side), fltp"
    private static readonly Regex regAudioLayout = new Regex(
      @"Stream #[^:]+:[^:]+: Audio: [^,]+, [0-9]+ Hz, ([^,\r\n]+)",
      RegexOptions.Compiled);

    private static readonly Regex regChannelCount = new Regex(
      @"^([0-9]+) channels", RegexOptions.Compiled);

    private static readonly Regex regChannelLayout = new Regex(
      @"^([0-9]+)\.([0-9]+)", RegexOptions.Compiled);

    private static readonly TimeSpan remuxTimeout = TimeSpan.FromHours(1);

    private static readonly TimeSpan moveTimeout = TimeSpan.FromMinutes(1);

    // ffmpeg muxer for each output extension
    private static readonly Dictionary<string, string> muxers =
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        {".mkv", "matroska"},
        {".mk3d", "matroska"},
        {".webm", "webm"},
        {".mp4", "mp4"},
        {".m4v", "mp4"},
        {".mov", "mov"},
        {".avi", "avi"},
        {".ts", "mpegts"},
        {".m2ts", "mpegts"},
        {".mts", "mpegts"},
        {".mpg", "mpeg"},
        {".mpeg", "mpeg"},
        {".wmv", "asf"},
        {".asf", "asf"},
        {".flv", "flv"},
        {".3gp", "3gp"},
        {".ogv", "ogg"}
      };

    public static readonly string FFmpegExecutable =
          FindExecutable("ffmpeg");
    private static DirectoryInfo GetFFMpegFolder(
      Environment.SpecialFolder folder)
    {
      return new DirectoryInfo(
        Path.Combine(Environment.GetFolderPath(folder), "ffmpeg"));
    }

    private static string FindExecutable(string executable)
    {
      var os = Environment.OSVersion.Platform.ToString().ToUpperInvariant();
      var isWin = os.Contains("WIN");
      if (isWin)
      {
        executable += ".exe";
      }
      var places = new List<DirectoryInfo>();
      var assemblyLoc = Assembly.GetExecutingAssembly().Location;
      if (assemblyLoc != null)
      {
        places.Add(new FileInfo(assemblyLoc).Directory);
      }
      try
      {
        var ffhome = Environment.GetEnvironmentVariable("FFMPEG_HOME");
        if (!string.IsNullOrWhiteSpace(ffhome))
        {
          places.Add(new DirectoryInfo(ffhome));
        }
      }
      catch (Exception)
      {
        // ignored
      }

      foreach (var l in specialLocations)
      {
        try
        {
          places.Add(l);
        }
        catch (Exception)
        {
          // ignored
        }
      }
      var envpath = Environment.GetEnvironmentVariable("PATH");
      if (!string.IsNullOrWhiteSpace(envpath))
      {
        foreach (var p in envpath.
          Split(isWin ? ';' : ':'))
        {
          try
          {
            places.Add(new DirectoryInfo(p.Trim()));
          }
          catch (Exception)
          {
            // ignored
          }
        }
      }

      foreach (var i in places)
      {
        LogManager.GetLogger(typeof(FFmpeg)).DebugFormat(
          "Searching {0}", i.FullName);
        if (!i.Exists)
        {
          continue;
        }
        var folders = new[]
        {
          i,
          new DirectoryInfo(Path.Combine(i.FullName, "bin"))
        };
        foreach (var di in folders)
        {
          try
          {
            var r = new FileInfo[0];
            if (di.Exists)
            {
              r = di.GetFiles(executable, SearchOption.TopDirectoryOnly);
            }
            if (r.Length != 0)
            {
              var rv = r[0];
              LogManager.GetLogger(typeof(FFmpeg)).InfoFormat(
                "Found {0} at {1}",
                executable,
                rv.FullName
                );
              return rv.FullName;
            }
          }
          catch (Exception)
          {
            // ignored
          }
        }
      }
      LogManager.GetLogger(typeof(FFmpeg)).WarnFormat(
        "Did not find {0}", executable);
      return null;
    }

    private static IDictionary<string, string> IdentifyFileInternal(
      FileInfo file)
    {
      if (FFmpegExecutable == null)
      {
        throw new NotSupportedException();
      }
      if (file == null)
      {
        throw new ArgumentNullException(nameof(file));
      }
      IDictionary<string, string> rv;
      if (infoCache.TryGetValue(file, out rv))
      {
        return rv;
      }
      try
      {
        return IdentifyInternalFromProcess(file);
      }
      catch (Exception ex)
      {
        throw new NotSupportedException(ex.Message, ex);
      }
    }

    private static IDictionary<string, string> IdentifyInternalFromProcess(
      FileInfo file)
    {
      using (var p = new Process())
      {
        var sti = p.StartInfo;
#if !DEBUG
        sti.CreateNoWindow = true;
#endif
        sti.UseShellExecute = false;
        sti.FileName = FFmpegExecutable;
        sti.Arguments = $"-i \"{file.FullName}\"";
        sti.LoadUserProfile = false;
        sti.RedirectStandardError = true;
        p.Start();
        IDictionary<string, string> rv = new Dictionary<string, string>();

        using (var reader = new StreamReader(StreamManager.GetStream()))
        {
          using (var pump = new StreamPump(
            p.StandardError.BaseStream, reader.BaseStream, 4096))
          {
            pump.Pump(null);
            if (!p.WaitForExit(3000))
            {
              throw new NotSupportedException("ffmpeg timed out");
            }
            if (!pump.Wait(1000))
            {
              throw new NotSupportedException("ffmpeg pump timed out");
            }
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            var output = reader.ReadToEnd();
            var match = regDuration.Match(output);
            if (match.Success)
            {
              int h, m, s;
              if (int.TryParse(match.Groups[1].Value, out h) &&
                  int.TryParse(match.Groups[2].Value, out m) &&
                  int.TryParse(match.Groups[3].Value, out s))
              {
                int ms;
                if (match.Groups.Count < 5 ||
                    !int.TryParse(match.Groups[4].Value, out ms))
                {
                  ms = 0;
                }
                var ts = new TimeSpan(0, h, m, s, ms * 10);
                var tss = ts.TotalSeconds.ToString(
                  CultureInfo.InvariantCulture);
                rv.Add("LENGTH", tss);
              }
            }
            match = regDimensions.Match(output);
            if (match.Success)
            {
              int w, h;
              if (int.TryParse(match.Groups[1].Value, out w) &&
                  int.TryParse(match.Groups[2].Value, out h))
              {
                rv.Add("VIDEO_WIDTH", w.ToString());
                rv.Add("VIDEO_HEIGHT", h.ToString());
              }
            }
            // First audio stream only
            match = regAudioLayout.Match(output);
            if (match.Success)
            {
              var channels = ParseChannels(match.Groups[1].Value);
              if (channels > 0)
              {
                rv.Add("AUDIO_CHANNELS", channels.ToString());
              }
            }
          }
        }
        if (rv.Count == 0)
        {
          throw new NotSupportedException("File not supported");
        }
        return rv;
      }
    }

    public static Size GetFileDimensions(FileInfo file)
    {
      string sw, sh;
      int w, h;
      if (IdentifyFile(file).TryGetValue("VIDEO_WIDTH", out sw)
          && IdentifyFile(file).TryGetValue("VIDEO_HEIGHT", out sh)
          && int.TryParse(sw, out w)
          && int.TryParse(sh, out h)
          && w > 0 && h > 0)
      {
        return new Size(w, h);
      }
      throw new NotSupportedException();
    }

    public static double GetFileDuration(FileInfo file)
    {
      string sl;
      if (IdentifyFile(file).TryGetValue("LENGTH", out sl))
      {
        double dur;
        var valid = double.TryParse(
          sl, NumberStyles.AllowDecimalPoint,
          CultureInfo.GetCultureInfo("en-US", "en"), out dur);
        if (valid && dur > 0)
        {
          return dur;
        }
      }
      throw new NotSupportedException();
    }

    public static string GetSubtitleSubrip(FileInfo file, bool externalFile)
    {
      if (externalFile)
      {
        return File.ReadAllText(file.FullName);
      }

      if (FFmpegExecutable == null)
      {
        throw new NotSupportedException();
      }
      if (file == null)
      {
        throw new ArgumentNullException(nameof(file));
      }
      try
      {
        using (var p = new Process())
        {
          var sti = p.StartInfo;
#if !DEBUG
          sti.CreateNoWindow = true;
#endif
          sti.UseShellExecute = false;
          sti.FileName = FFmpegExecutable;
          sti.Arguments = $"-i \"{file.FullName}\" -map s:0 -f srt pipe:";
          sti.LoadUserProfile = false;
          sti.RedirectStandardOutput = true;
          p.Start();

          var lastPosition = 0L;
          using (var reader = new StreamReader(StreamManager.GetStream()))
          {
            using (var pump = new StreamPump(
              p.StandardOutput.BaseStream, reader.BaseStream, 100))
            {
              pump.Pump(null);
              while (!p.WaitForExit(20000))
              {
                if (lastPosition != reader.BaseStream.Position)
                {
                  lastPosition = reader.BaseStream.Position;
                  continue;
                }
                p.Kill();
                throw new NotSupportedException("ffmpeg timed out");
              }
              if (!pump.Wait(2000))
              {
                throw new NotSupportedException("ffmpeg pump timed out");
              }
              reader.BaseStream.Seek(0, SeekOrigin.Begin);

              var rv = string.Empty;
              string line;
              while ((line = reader.ReadLine()) != null)
              {
                rv += regAssStrip.Replace(line.Trim(), string.Empty) + "\n";
              }
              if (!string.IsNullOrWhiteSpace(rv))
              {
                return rv;
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        throw new NotSupportedException(ex.Message, ex);
      }
      throw new NotSupportedException(
        "File does not contain a valid subtitle");
    }

    private static int ParseChannels(string layout)
    {
      layout = layout.Trim().ToLowerInvariant();
      if (layout == "mono")
      {
        return 1;
      }
      if (layout.StartsWith("stereo", StringComparison.Ordinal) ||
          layout == "downmix")
      {
        return 2;
      }
      var match = regChannelCount.Match(layout);
      if (match.Success)
      {
        return int.Parse(match.Groups[1].Value);
      }
      // 5.1 -> 6, 7.1(wide) -> 8, 3.0 -> 3
      match = regChannelLayout.Match(layout);
      if (match.Success)
      {
        return int.Parse(match.Groups[1].Value) +
               int.Parse(match.Groups[2].Value);
      }
      switch (layout)
      {
        case "quad":
        case "quad(side)":
          return 4;
        case "hexagonal":
          return 6;
        case "octagonal":
          return 8;
        default:
          return 0;
      }
    }

    /// <summary>
    ///   Channel count of the first audio stream, or 0 if unknown
    /// </summary>
    public static int GetFirstAudioChannels(FileInfo file)
    {
      string sc;
      int channels;
      if (IdentifyFile(file).TryGetValue("AUDIO_CHANNELS", out sc) &&
          int.TryParse(sc, out channels))
      {
        return channels;
      }
      return 0;
    }

    /// <summary>
    ///   Copies a file to output, converting the first audio stream to AC3
    ///   stereo. All other streams are copied unchanged where the container
    ///   allows it; otherwise only the first video and audio are kept.
    ///   The output container is chosen from the output's extension.
    /// </summary>
    public static void RemuxWithStereoAudio(FileInfo source, FileInfo output)
    {
      if (FFmpegExecutable == null)
      {
        throw new NotSupportedException("ffmpeg not found");
      }
      if (source == null)
      {
        throw new ArgumentNullException(nameof(source));
      }
      if (output == null)
      {
        throw new ArgumentNullException(nameof(output));
      }
      // AC3 encodes about 4x faster than ffmpeg's built-in AAC encoder.
      // For clients that can't play AC3, use AAC instead:
      //   "-c:a:0 aac -ac:a:0 2 -b:a:0 192k -max_muxing_queue_size 4096"
      const string stereo =
        "-c:a:0 ac3 -ac:a:0 2 -b:a:0 192k -max_muxing_queue_size 4096";
      var attempts = new[]
      {
        // Everything, as the container allows (keeps subtitles etc.)
        $"-map 0 -c copy {stereo}",
        // Just the main video and audio
        $"-map 0:v:0? -map 0:a:0 -c copy {stereo}"
      };
      // Write to a name that doesn't look like a video, so media scanners
      // (including our own folder watcher) leave it alone until it's done.
      // ffmpeg then needs telling the container, as it can't guess it.
      string temp, format;
      string muxer;
      if (muxers.TryGetValue(output.Extension, out muxer))
      {
        temp = output.FullName + ".partial";
        format = $"-f {muxer}";
      }
      else
      {
        temp = Path.Combine(
          output.DirectoryName ?? string.Empty,
          Path.GetFileNameWithoutExtension(output.Name) + ".partial" +
          output.Extension);
        format = string.Empty;
      }
      var log = LogManager.GetLogger(typeof(FFmpeg));
      string errors = null;
      foreach (var args in attempts)
      {
        errors = RunFFmpeg(
          $"-nostdin -hide_banner -loglevel error -y -i \"{source.FullName}\" {args} {format} \"{temp}\"");
        if (errors == null)
        {
          ReplaceWithRetry(temp, output.FullName);
          return;
        }
        log.DebugFormat("ffmpeg {0} failed for {1}: {2}", args, source.FullName, errors);
      }
      try
      {
        File.Delete(temp);
      }
      catch (Exception)
      {
        // ignored
      }
      throw new NotSupportedException($"ffmpeg failed: {errors}");
    }

    /// <summary>
    ///   Moves source over destination. A newly written file is often held
    ///   open for a while by virus scanners, indexers or thumbnailers, so
    ///   keep trying for a minute before giving up.
    /// </summary>
    private static void ReplaceWithRetry(string source, string destination)
    {
      var deadline = DateTime.UtcNow + moveTimeout;
      var delay = 250;
      for (;;)
      {
        try
        {
          if (File.Exists(destination))
          {
            File.Delete(destination);
          }
          File.Move(source, destination);
          return;
        }
        catch (Exception ex) when (
          (ex is IOException || ex is UnauthorizedAccessException) &&
          DateTime.UtcNow < deadline)
        {
          LogManager.GetLogger(typeof(FFmpeg)).DebugFormat(
            "{0} is in use, retrying in {1}ms: {2}", source, delay,
            ex.Message);
          System.Threading.Thread.Sleep(delay);
          delay = Math.Min(delay * 2, 4000);
        }
      }
    }

    /// <summary>
    ///   Runs ffmpeg to completion. Returns null on success, otherwise the
    ///   error output.
    /// </summary>
    private static string RunFFmpeg(string arguments)
    {
      using (var p = new Process())
      {
        var sti = p.StartInfo;
        sti.CreateNoWindow = true;
        sti.UseShellExecute = false;
        sti.FileName = FFmpegExecutable;
        sti.Arguments = arguments;
        sti.LoadUserProfile = false;
        sti.RedirectStandardError = true;
        var errors = new System.Text.StringBuilder();
        p.ErrorDataReceived += (s, e) =>
        {
          if (e.Data != null)
          {
            lock (errors)
            {
              errors.AppendLine(e.Data);
            }
          }
        };
        p.Start();
        p.BeginErrorReadLine();
        if (!p.WaitForExit((int)remuxTimeout.TotalMilliseconds))
        {
          p.Kill();
          return "ffmpeg timed out";
        }
        // Flush the asynchronous error reader
        p.WaitForExit();
        lock (errors)
        {
          return p.ExitCode == 0
            ? null
            : $"exit code {p.ExitCode}: {errors.ToString().Trim()}";
        }
      }
    }

    public static IDictionary<string, string> IdentifyFile(FileInfo file)
    {
      if (FFmpegExecutable != null)
      {
        return IdentifyFileInternal(file);
      }
      throw new NotSupportedException();
    }
  }
}
