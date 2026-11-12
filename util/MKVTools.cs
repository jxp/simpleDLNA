using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMaier.SimpleDlna.Utilities
{
  public static class MKVTools
  {
    private static string mkvDirectory = null;
    private static string mkvMergeExecutable;
    private static string mkvExtractExe;
    public static bool Loaded = false;

    private enum AttachmentPriority
    {
      LargeLandscape = 0,
      LargePortrait = 1,
      SmallLandscape = 2,
      SmallPortrait = 3
    }

    public const int NoThumbnail = -1;

    public static void Initialise(string mkvFolder)
    {
      mkvDirectory = mkvFolder;

      if (string.IsNullOrEmpty(mkvFolder))
      {
        // Log no mkv Tools
        Loaded = false;
        return;
      }

      mkvMergeExecutable = Path.Combine(mkvFolder, "mkvmerge.exe");
      mkvExtractExe = Path.Combine(mkvFolder, "mkvextract.exe");

      if (File.Exists(mkvMergeExecutable) && File.Exists(mkvExtractExe))
            {
        Loaded = true;
            }

    }

    public static int FindThumbnail(string mkvFile)
    {
      if (!string.IsNullOrEmpty(mkvDirectory))
      {
        using (var p = new Process())
        {
          var sti = p.StartInfo;
          sti.UseShellExecute = false;
          sti.FileName = mkvMergeExecutable;
          sti.Arguments = string.Format("--identify \"{0}\"", mkvFile);
          sti.LoadUserProfile = false;
          sti.RedirectStandardOutput = true;
          sti.WindowStyle = ProcessWindowStyle.Minimized;
          p.Start();

          using (var reader = new StreamReader(StreamManager.GetStream()))
          {
            using (var pump = new StreamPump(
              p.StandardOutput.BaseStream, reader.BaseStream, 4096))
            {
              pump.Pump(null);
              if (!p.WaitForExit(2000))
              {
                if (!p.HasExited)
                {
                  p.Kill();
                }
                throw new TimeoutException("mkvtools timed out");
              }
              if (!pump.Wait(1000))
              {
                if (!p.HasExited)
                {
                  p.Kill();
                }
                throw new NotSupportedException("mkvtools pump timed out");
              }
              if (p.ExitCode != 0)
              {
                p.Kill();
                throw new ArgumentException("mkvMerge does not understand the stream");
              }

              reader.BaseStream.Seek(0, SeekOrigin.Begin);

              var output = reader.ReadToEnd();

              var outLines = output.Split('\n');
              var idxOrder = 4;
              var matchedIndex = NoThumbnail;
              foreach (var line in outLines)
              {
                if (line.StartsWith("Attachment") && line.Contains("cover"))
                {
                  // Get the attachment index and the type
                  var idPos = line.IndexOf("ID");
                  int idx = int.Parse(line.Substring(idPos + 3, line.IndexOf(":", idPos) - idPos - 3));
                  var namePos = line.IndexOf("file name");
                  var newIndex = idxOrder;
                  var coverName = line.Substring(namePos + 11, line.IndexOf("'", namePos + 11) - namePos - 11);
                  switch (coverName)
                  {
                    case "cover_land.jpg":
                    case "cover_land.png":
                      newIndex = (int)AttachmentPriority.LargeLandscape;
                      break;
                    case "cover.jpg":
                    case "cover.png":
                      newIndex = (int)AttachmentPriority.LargePortrait;
                      break;
                    case "small_cover_land.jpg":
                    case "small_cover_land.png":
                      newIndex = (int)AttachmentPriority.SmallLandscape;
                      break;
                    case "small_cover.jpg":
                    case "small_cover.png":
                      newIndex = (int)AttachmentPriority.SmallPortrait;
                      break;
                    default:
                      // Unknown attachment
                      break;
                  }

                  if (newIndex < idxOrder)
                  {
                    matchedIndex = idx;
                    idxOrder = newIndex;
                  }



                }
              }
              if (!p.HasExited)
              {
                p.Kill();
              }
              return matchedIndex;
            }

          }
          if (!p.HasExited)
          {
            p.Kill();
          }
        }
      }
      return NoThumbnail;

    }

    public static MemoryStream ExtractThumbnail(string mkvFile, int index)
    {
      if (!string.IsNullOrEmpty(mkvDirectory))
      {
        var tempFile = Path.GetTempFileName();
        using (var p = new Process())
        {
          var sti = p.StartInfo;
          sti.UseShellExecute = false;
          sti.FileName = mkvExtractExe;
          sti.Arguments = string.Format("attachments \"{0}\" {1}:{2}", mkvFile, index, tempFile);
          sti.LoadUserProfile = false;
          sti.RedirectStandardOutput = true;
          sti.WindowStyle = ProcessWindowStyle.Minimized;
          p.Start();

          using (var reader = new StreamReader(StreamManager.GetStream()))
          {
            using (var pump = new StreamPump(
              p.StandardOutput.BaseStream, reader.BaseStream, 4096))
            {
              pump.Pump(null);
              if (!p.WaitForExit(2000))
              {
                if (!p.HasExited)
                {
                  p.Kill();
                }
                throw new TimeoutException("mkvtools timed out");
              }
              if (!pump.Wait(1000))
              {
                if (!p.HasExited)
                {
                  p.Kill();
                }
                throw new NotSupportedException("mkvtools pump timed out");
              }
              if (p.ExitCode != 0)
              {
                p.Kill();
                throw new ArgumentException("mkvExtract does not understand the stream");
              }

              reader.BaseStream.Seek(0, SeekOrigin.Begin);

              var output = reader.ReadToEnd();
              if (output.Contains("is written to"))
              {
                MemoryStream fileContents = new MemoryStream();
                using (FileStream file = new FileStream(tempFile, FileMode.Open, FileAccess.Read))
                {
                  file.CopyTo(fileContents);
                }
                File.Delete(tempFile);
                if (!p.HasExited)
                {
                  p.Kill();
                }
                return fileContents;
              }
            }
            if (!p.HasExited)
            {
              p.Kill();
            }
          }
        }
          }
      return null;
    }
  }

}
