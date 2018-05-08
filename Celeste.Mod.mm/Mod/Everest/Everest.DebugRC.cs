using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod.Helpers;
using Celeste.Mod.Core;
using System.Net;
using System.Threading;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class DebugRC {

            private static HttpListener Listener;

            #region HttpListener

            public static void Initialize() {
                if (Listener != null)
                    return;

                int port;
                if (Celeste.PlayMode != Celeste.PlayModes.Debug ||
                    !int.TryParse(CoreModule.Settings.DebugRCPort, out port))
                    return;

                Listener = new HttpListener();
                Listener.Prefixes.Add($"http://localhost:{port}/");
                Listener.Start();

                ThreadPool.QueueUserWorkItem(_ => {
                    Logger.Log(LogLevel.Info, "debugrc", $"Started DebugRC thread, available via http://localhost:{port}/");
                    try {
                        while (Listener.IsListening) {
                            ThreadPool.QueueUserWorkItem(c => {
                                HttpListenerContext context = c as HttpListenerContext;
                                try {
                                    using (context.Request.InputStream)
                                    using (context.Response) {
                                        HandleRequest(context);
                                    }
                                } catch (Exception e) {
                                    Logger.Log("debugrc", "DebugRC failed responding: " + e);
                                }
                            }, Listener.GetContext());
                        }
                    } catch (Exception e) {
                        Logger.Log("debugrc", "DebugRC failed listening: " + e);
                    }
                });
            }

            private static void HandleRequest(HttpListenerContext c) {
                Logger.Log(LogLevel.Verbose, "debugrc", $"Requested: {c.Request.RawUrl}");

                RCEndPoint endpoint =
                    EndPoints.FirstOrDefault(ep => ep.Path == c.Request.RawUrl) ??
                    EndPoints.FirstOrDefault(ep => ep.Path.ToLowerInvariant() == c.Request.RawUrl.ToLowerInvariant()) ??
                    EndPoints.FirstOrDefault(ep => ep.Path == "/404");
                endpoint.Handle(c);
            }

            #endregion

            #region Write Helpers

            public static void WriteHTMLStart(HttpListenerContext c, StringBuilder builder) {
                builder.Append(
@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"" />
        <meta name=""viewport"" content=""width=device-width, initial-scale=1, user-scalable=no"" />
        <title>Everest DebugRC</title>
        <style>

@font-face {
    font-family: Renogare;
    src:
    url(""https://everestapi.github.io/fonts/Renogare-Regular.woff"") format(""woff""),
    url(""https://everestapi.github.io/fonts/Renogare-Regular.otf"") format(""opentype"");
}

body {
    color: rgba(0, 0, 0, 0.87);
    font-family: sans-serif;
    padding: 0;
    margin: 0;
    line-height: 1.5em;
}

header {
    background: #3b2d4a;
    color: white;
    font-family: Renogare, sans-serif;
    font-size: 32px;
    position: sticky;
    top: 0;
    left: 0;
    right: 0;
    height: 64px;
    line-height: 64px;
    padding: 8px 48px;
    z-index: 100;
}

#main {
    position: relative;
    margin: 8px;
    min-height: 100vh;
}

#endpoints li h3 {
    margin-bottom: 0;
}
#endpoints li p {
    margin-top: 0;
}

        </style>
    </head>
    <body>


"
                );

                builder.AppendLine(@"<header>Everest DebugRC Server</header>");
                builder.AppendLine(@"<div id=""main"">");
            }

            public static void WriteHTMLEnd(HttpListenerContext c, StringBuilder builder) {
                builder.AppendLine(@"</div>");

                builder.Append(
@"

    </body>
</html>
"
                );
            }

            public static void Write(HttpListenerContext c, string str) {
                byte[] buf = Encoding.UTF8.GetBytes(str);
                c.Response.ContentLength64 = buf.Length;
                c.Response.OutputStream.Write(buf, 0, buf.Length);
            }

            #endregion

            #region Default RCEndPoint Handlers

            public static List<RCEndPoint> EndPoints = new List<RCEndPoint> {

                new RCEndPoint {
                    Path = "/",
                    Name = "Info",
                    InfoHTML = "Basic game info.",
                    Handle = c => {
                        StringBuilder builder = new StringBuilder();

                        WriteHTMLStart(c, builder);

                        builder.AppendLine(@"<ul>");
                        builder.AppendLine(@"<h2>Game</h2>");
                        builder.AppendLine($@"<li><b>Celeste</b> v.{Celeste.Instance.Version}</li>");
                        foreach (EverestModule mod in _Modules) {
                            builder.AppendLine($@"<li><b>{mod.Metadata.Name}</b> v.{mod.Metadata.VersionString}</li>");
                        }
                        builder.AppendLine(@"</ul>");

                        builder.AppendLine(@"<ul id=""endpoints"">");
                        builder.AppendLine(@"<h2>Endpoints</h2>");
                        foreach (RCEndPoint ep in EndPoints) {
                            builder.AppendLine(@"<li>");
                            builder.AppendLine($@"<h3>{ep.Name}</h3>");
                            builder.AppendLine($@"<p><code>{ep.Path}</code><br>{ep.InfoHTML}</p>");
                            builder.AppendLine(@"</li>");
                        }
                        builder.AppendLine(@"</ul>");

                        WriteHTMLEnd(c, builder);

                        Write(c, builder.ToString());
                    }
                },

                new RCEndPoint {
                    Path = "/404",
                    Name = "404",
                    InfoHTML = "Basic 404 endpoint.",
                    Handle = c => {
                        c.Response.StatusCode = (int) HttpStatusCode.NotFound;
                        Write(c, "ERROR: 404 - Endpoint not found.");
                    }
                },

            };

            #endregion

        }
    }
}
