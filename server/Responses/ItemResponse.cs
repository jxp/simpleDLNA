using System;
using System.IO;
using NMaier.SimpleDlna.Server.Metadata;
using NMaier.SimpleDlna.Utilities;

namespace NMaier.SimpleDlna.Server
{
  internal sealed class ItemResponse : Logging, IResponse
  {
    private readonly Headers headers;

    private readonly FileInfo content;

    private readonly IMediaResource item;

    public ItemResponse(string prefix, IRequest request, IMediaResource item,
      string transferMode = "Streaming")
      : this(prefix, request, item, null, transferMode)
    {
    }

    /// <summary>
    ///   Serves content (e.g. a transcoded copy) in place of the item's own
    ///   data, while describing it as the item.
    /// </summary>
    public ItemResponse(string prefix, IRequest request, IMediaResource item,
      FileInfo content, string transferMode = "Streaming")
    {
      this.item = item;
      this.content = content;
      headers = new ResponseHeaders(!(item is IMediaCoverResource));
      var meta = item as IMetaInfo;
      if (content != null) {
        headers.Add("Content-Length", content.Length.ToString());
        headers.Add("Last-Modified", content.LastWriteTimeUtc.ToString("R"));
      }
      else if (meta != null) {
        headers.Add("Content-Length", meta.InfoSize.ToString());
        headers.Add("Last-Modified", meta.InfoDate.ToString("R"));
      }
      headers.Add("Accept-Ranges", "bytes");
      headers.Add("Content-Type", DlnaMaps.Mime[item.Type]);

      if (request.Headers.ContainsKey("getcontentFeatures.dlna.org")) {
        try {
          headers.Add(
            "contentFeatures.dlna.org",
            item.MediaType == DlnaMediaTypes.Image
              ? $"DLNA.ORG_PN={item.PN};DLNA.ORG_OP=00;DLNA.ORG_CI=0;DLNA.ORG_FLAGS={DlnaMaps.DefaultInteractive}"
              : $"DLNA.ORG_PN={item.PN};DLNA.ORG_OP=01;DLNA.ORG_CI=0;DLNA.ORG_FLAGS={DlnaMaps.DefaultStreaming}"
            );
        }
        catch (NotSupportedException) {
        }
        catch (NotImplementedException) {
        }
      }
      if (request.Headers.ContainsKey("getCaptionInfo.sec")) {
        var mvi = item as IMetaVideoItem;
        if (mvi != null && mvi.Subtitle.HasSubtitle) {
          var surl =
            $"http://{request.LocalEndPoint.Address}:{request.LocalEndPoint.Port}{prefix}subtitle/{item.Id}/st.srt";
          DebugFormat("Sending subtitles {0}", surl);
          headers.Add("CaptionInfo.sec", surl);
        }
      }
      if (request.Headers.ContainsKey("getMediaInfo.sec")) {
        var md = item as IMetaDuration;
        if (md?.MetaDuration != null) {
          headers.Add(
            "MediaInfo.sec",
            $"SEC_Duration={md.MetaDuration.Value.TotalMilliseconds};"
            );
        }
      }
      headers.Add("transferMode.dlna.org", transferMode);

      Debug(headers);
    }

    public Stream Body => content == null
      ? item.CreateContentStream()
      : new FileStream(
        content.FullName, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete);

    public IHeaders Headers => headers;

    public HttpCode Status { get; } = HttpCode.Ok;
  }
}
