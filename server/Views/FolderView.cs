using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMaier.SimpleDlna.Server.Views
{
  internal sealed class FolderView : BaseView
  {
    public override string Description => "View files in the disk folder structure";

    public override string Name => "folder";

    private static bool TransformInternal(VirtualFolder root,
      VirtualFolder current)
    {
      foreach (var f in current.ChildFolders.ToList())
      {
        var vf = f as VirtualFolder;
        if (TransformInternal(root, vf))
        {
          current.ReleaseFolder(vf);
        }
      }

      if (current == root || current.ChildItems.Count() > 3)
      {
        return false;
      }
      var newParent = (VirtualFolder)current.Parent;
      foreach (var c in current.ChildItems.ToList())
      {
        current.RemoveResource(c);
        newParent.AddResource(c);
      }

      if (current.ChildCount != 0)
      {
        MergeFolders(current, newParent);
        foreach (var f in current.ChildFolders.ToList())
        {
          newParent.AdoptFolder(f);
        }
        foreach (var f in current.ChildItems.ToList())
        {
          current.RemoveResource(f);
          newParent.AddResource(f);
        }
      }
      return true;
    }

    public override IMediaFolder Transform(IMediaFolder oldRoot)
    {
      var r = new VirtualClonedFolder(oldRoot);

      // As this is a standard folder view there is no need to merge any folders
      //  each one is considered unique due to its path
      // i.e. Videos\Family\2022 should not be merged with Videos\School\2022
/*      var cross = from f in r.ChildFolders
                  from t in r.ChildFolders
                  where f != t
                  orderby f.Title, t.Title
                  select new
                  {
                    f = f as VirtualFolder,
                    t = t as VirtualFolder
                  };
      foreach (var c in cross)
      {
        MergeFolders(c.f, c.t);
      }

      //TransformInternal(r, r);
      MergeFolders(r, r);
*/
      return r;
    }
  }

}
