using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NMaier.SimpleDlna.Server
{
  /// <summary>
  ///   Reads a file that is still being written (a stereo copy in progress),
  ///   waiting for more data until the writer has finished. Its length isn't
  ///   known, so it can't seek.
  /// </summary>
  internal sealed class GrowingFileStream : Stream
  {
    private static readonly TimeSpan stallTimeout = TimeSpan.FromMinutes(5);

    private readonly FileStream file;

    // Completes with the finished file, or null if writing it failed
    private readonly Task<FileInfo> writer;

    private DateTime lastGrowth = DateTime.Now;

    private long position;

    public GrowingFileStream(string path, Task<FileInfo> writer)
    {
      this.writer = writer;
      // Allow the writer to keep writing, and to rename it when done
      file = new FileStream(
        path, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete, 1 << 16);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length
    {
      get { throw new NotSupportedException("Still being written"); }
    }

    public override long Position
    {
      get { return position; }
      set { throw new NotSupportedException(); }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      for (;;) {
        // ffmpeg starts again from scratch if its first attempt fails
        if (file.Length < position) {
          throw new IOException("File was rewritten while streaming it");
        }
        var read = file.Read(buffer, offset, count);
        if (read > 0) {
          position += read;
          lastGrowth = DateTime.Now;
          return read;
        }
        if (writer.IsCompleted) {
          if (writer.Status != TaskStatus.RanToCompletion ||
              writer.Result == null) {
            throw new IOException("Writing the file failed");
          }
          // Anything written just before finishing
          read = file.Read(buffer, offset, count);
          position += read;
          return read;
        }
        if (DateTime.Now - lastGrowth > stallTimeout) {
          throw new IOException("File stopped growing");
        }
        Thread.Sleep(250);
      }
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing) {
        file.Dispose();
      }
      base.Dispose(disposing);
    }
  }
}
