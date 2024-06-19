using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;

namespace Celeste.Mod.Helpers {
    /// <summary>
    /// An HttpClient that supports compressed responses to save bandwidth, and uses IPv4 to work around issues for some users.
    /// </summary>
    public class CompressedHttpClient : HttpClient {
        private static SocketsHttpHandler handler = new SocketsHttpHandler() {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = async delegate (SocketsHttpConnectionContext ctx, CancellationToken token) {
                if (ctx.DnsEndPoint.AddressFamily != AddressFamily.Unspecified && ctx.DnsEndPoint.AddressFamily != AddressFamily.InterNetwork) {
                    throw new InvalidOperationException("no IPv4 address");
                }

                Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try {
                    await socket.ConnectAsync(new DnsEndPoint(ctx.DnsEndPoint.Host, ctx.DnsEndPoint.Port, AddressFamily.InterNetwork), token).ConfigureAwait(false);
                    return new NetworkStream(socket, true);
                } catch (Exception) {
                    socket.Dispose();
                    throw;
                }
            }
        };

        public CompressedHttpClient() : base(handler, disposeHandler: false) {
            DefaultRequestHeaders.Add("User-Agent", $"Everest/{Everest.VersionString}");
        }
    }
}
