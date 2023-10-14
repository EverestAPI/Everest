using System;
using System.Net;

namespace Celeste.Mod.Helpers {
    /// <summary>
    /// A WebClient that supports gzip-compressed responses to save bandwidth.
    /// </summary>
    public class CompressedWebClient : WebClient {
        protected override WebRequest GetWebRequest(Uri address) {
            HttpWebRequest request = (HttpWebRequest) base.GetWebRequest(address);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.UserAgent = "Everest/" + Everest.VersionString;
            return request;
        }
    }
}
