using System.Collections.Generic;
using System.Linq;

namespace NMaier.SimpleDlna.Server
{
  public interface IMediaFolder : IMediaItem
  {
    int ChildCount { get; }

    int FullChildCount { get; }

    IEnumerable<IMediaFolder> ChildFolders { get; }

    IEnumerable<IMediaResource> ChildItems { get; }

    IMediaFolder Parent { get; set; }

    void AddResource(IMediaResource res);

    void Cleanup();

    bool RemoveResource(IMediaResource res);

    void Sort(IComparer<IMediaItem> sortComparer, bool descending);
  }

  public static class FolderExtensions
  {
    public static bool RecursiveSearchItem(this IMediaFolder master, string itemID)
    {
      if (master.ChildItems.Any(c => c.Id == itemID))
      {
        return true;
      }
      foreach (var folder in master.ChildFolders)
      {
        if (folder.RecursiveSearchItem(itemID))
        {
          return true;
        }
      }
      return false;
    }

    public static bool RecursiveMatchPath(this IMediaFolder master, string path)
    {
      if (path.StartsWith(master.Path))
      {
        return true;
      }

      if (master is VirtualFolder)
      {
        return ((VirtualFolder)master).MatchAnyPath(path);
      }
      else
      {
        foreach (var folder in master.ChildFolders)
        {
          if (folder.RecursiveMatchPath(path))
          {
            return true;
          }
        }
        return false;
     }
    }
  }
}
