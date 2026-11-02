using System.Net;

namespace NMaier.SimpleDlna.Server
{
  public interface IRequest
  {
    string Body { get; }

    /// <summary>
    ///   The client making the request, or null if it sent no User-Agent
    /// </summary>
    ClientInfo Client { get; }

    IHeaders Headers { get; }

    IPEndPoint LocalEndPoint { get; }

    string Method { get; }

    string Path { get; }

    IPEndPoint RemoteEndpoint { get; }
  }
}
