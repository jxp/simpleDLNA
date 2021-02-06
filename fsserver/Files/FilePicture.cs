using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;

namespace NMaier.SimpleDlna.FileMediaServer.Files
{
  internal class FilePicture : IPicture
  {
    private readonly FileInfo PictureFile;
    private byte[] fileContent;

    internal FilePicture(FileInfo source)
    {
      PictureFile = source;
    }

    public string MimeType {
      get { return "image/jpeg"; }
      set { }
    }
    public PictureType Type
    {
      get { return PictureType.FrontCover; }
      set { }
    }
    public string Description {
      get { return "thumbnail"; }
      set { }
    }
    public ByteVector Data {
      get {
        if (fileContent == null)
        {
          fileContent = System.IO.File.ReadAllBytes(PictureFile.FullName);
        }
        return new ByteVector(fileContent);
      }
      set { }
    }
  }
}
