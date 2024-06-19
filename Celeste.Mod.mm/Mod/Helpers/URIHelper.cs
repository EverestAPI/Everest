using System.Collections.Specialized;
using System.Linq;

namespace Celeste.Mod.Helpers {
    public class URIHelper {

        public readonly string Uri;

        public URIHelper(string uri, NameValueCollection queryParams) {
            Uri = uri + '?' + ToQueryString(queryParams);
        }

        public override string ToString() => Uri;

        public static string ToQueryString(NameValueCollection nvc) {
            return string.Join("&",
                from key in nvc.AllKeys
                from value in nvc.GetValues(key)
                    select string.Format("{0}={1}", key, value));
        }

    }
}