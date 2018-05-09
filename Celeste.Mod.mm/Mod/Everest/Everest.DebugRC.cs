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
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class DebugRC {

            private static HttpListener Listener;

            #region HttpListener

            public static void Initialize() {
                if (Listener != null)
                    return;

                if (Celeste.PlayMode != Celeste.PlayModes.Debug ||
                    CoreModule.Settings.DebugRCPort <= 0)
                    return;

                Listener = new HttpListener();
                Listener.Prefixes.Add($"http://localhost:{CoreModule.Settings.DebugRCPort}/");
                Listener.Start();

                ThreadPool.QueueUserWorkItem(_ => {
                    Logger.Log(LogLevel.Info, "debugrc", $"Started DebugRC thread, available via http://localhost:{CoreModule.Settings.DebugRCPort}/");
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

                string url = c.Request.RawUrl;
                int indexOfSplit = url.IndexOf('?');
                if (indexOfSplit != -1)
                    url = url.Substring(0, indexOfSplit);

                RCEndPoint endpoint =
                    EndPoints.FirstOrDefault(ep => ep.Path == c.Request.RawUrl) ??
                    EndPoints.FirstOrDefault(ep => ep.Path == url) ??
                    EndPoints.FirstOrDefault(ep => ep.Path.ToLowerInvariant() == c.Request.RawUrl.ToLowerInvariant()) ??
                    EndPoints.FirstOrDefault(ep => ep.Path.ToLowerInvariant() == url.ToLowerInvariant()) ??
                    EndPoints.FirstOrDefault(ep => ep.Path == "/404");
                endpoint.Handle(c);
            }

            #endregion

            #region Read / Parse Helpers

            public static NameValueCollection ParseQueryString(string url) {
                NameValueCollection nvc = new NameValueCollection();

                int indexOfSplit = url.IndexOf('?');
                if (indexOfSplit == -1)
                    return nvc;
                url = url.Substring(indexOfSplit + 1);

                string[] args = url.Split('&');
                foreach (string arg in args) {
                    indexOfSplit = arg.IndexOf('=');
                    if (indexOfSplit == -1)
                        continue;
                    nvc[arg.Substring(0, indexOfSplit)] = arg.Substring(indexOfSplit + 1);
                }

                return nvc;
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
                            builder.AppendLine($@"<p><a href=""{ep.PathExample ?? ep.Path}""><code>{ep.PathHelp ?? ep.Path}</code></a><br>{ep.InfoHTML}</p>");
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
                    InfoHTML = "Basic 404.",
                    Handle = c => {
                        c.Response.StatusCode = (int) HttpStatusCode.NotFound;
                        Write(c, "ERROR: Endpoint not found.");
                    }
                },

                new RCEndPoint {
                    Path = "/focus",
                    Name = "Focus Game",
                    InfoHTML = "Refocus the game window. Doesn't work on Windows 10.",
                    Handle = c => {
                        if (SDL_RaiseWindow != null)
                            SDL_RaiseWindow(null, Celeste.Instance.Window.Handle);
                        else
                            SetForegroundWindow(Celeste.Instance.Window.Handle);

                        Write(c, "OK");
                    }
                },

                new RCEndPoint {
                    Path = "/respawn",
                    Name = "Respawn",
                    InfoHTML = "Restart the current screen, respawning the player.",
                    Handle = c => {
                        Level level = Engine.Scene as Level;
                        if (level == null) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, "ERROR: Player not in a level.");
                            return;
                        }

                        Engine.Scene = new LevelLoader(level.Session, level.Session.RespawnPoint);
                        Write(c, "OK");
                    }
                },

                new RCEndPoint {
                    Path = "/reloadmap",
                    PathHelp = "/reloadmap?area={none|SID}&side={none|A|B|C} (Example: ?sid=Celeste/1-ForsakenCity&side=A)",
                    PathExample = "/reloadmap?area=Celeste/1-ForsakenCity&side=A",
                    Name = "Reload Map",
                    InfoHTML = "Reload the map binary. Passing no side will reload all side binaries.",
                    Handle = c => {
                        NameValueCollection data = ParseQueryString(c.Request.RawUrl);

                        string sid = data["area"];
                        if (string.IsNullOrEmpty(sid))
                            sid = (Engine.Scene as Level)?.Session.Area.GetSID();
                        if (string.IsNullOrEmpty(sid)) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: No SID given.");
                            return;
                        }

                        AreaData area = AreaDataExt.Get(sid);
                        if (area == null) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: Chapter not found: {sid}");
                            return;
                        }

                        string sideStr = data["side"];
                        if (!string.IsNullOrEmpty(sideStr)) {
                            foreach (ModeProperties mode in area.Mode)
                                mode?.MapData?.Reload();
                            Write(c, "OK");
                            return;
                        }

                        int side = sideStr.Length != 1 ? -1 : (sideStr.ToLowerInvariant()[0] - 'a');
                        if (side < 0 || area.Mode.Length <= side) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: Invalid side value.");
                            return;
                        }

                        if (area.Mode[side] == null) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: Area {sid} doesn't have side {(char) ('A' + side)}.");
                        }

                        Write(c, "OK");
                    }
                },

                new RCEndPoint {
                    Path = "/tp",
                    PathHelp = "/tp?area={none|SID}&side={none|A|B|C}&level={LVL}&x={none|X}&y={none|Y}&forcenew={*|true} (Example: ?area=Celeste/1-ForsakenCity&side=A&level=8zb)",
                    PathExample = "/tp?area=Celeste/1-ForsakenCity&side=A&level=8zb",
                    Name = "Teleport To Map",
                    InfoHTML = "Teleport the player to the given level, reusing the session if possible. Set <code>forcenew=true</code> to start a new session.",
                    Handle = c => {
                        NameValueCollection data = ParseQueryString(c.Request.RawUrl);

                        if (SaveData.Instance == null)
                            SaveData.InitializeDebugMode();
                        Session session = (Engine.Scene as Level)?.Session;

                        string sid = data["area"];
                        if (string.IsNullOrEmpty(sid))
                            sid = session.Area.GetSID();
                        if (string.IsNullOrEmpty(sid)) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: No SID given.");
                            return;
                        }

                        AreaData area = AreaDataExt.Get(sid);
                        if (area == null) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: Chapter not found: {sid}");
                            return;
                        }

                        int side;
                        string sideStr = data["side"];
                        if (!string.IsNullOrEmpty(sideStr)) {
                            side = sideStr.Length != 1 ? -1 : (sideStr.ToLowerInvariant()[0] - 'a');
                            if (side < 0 || area.Mode.Length <= side) {
                                c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                                Write(c, $"ERROR: Invalid side value.");
                                return;
                            }
                        } else if (session != null) {
                            side = (int) session.Area.Mode;
                        } else {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: No side given.");
                            return;
                        }

                        string levelName = data["level"];
                        if (string.IsNullOrEmpty(levelName)) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: No level given.");
                        }

                        ModeProperties mode = area.Mode[side];
                        if (mode == null) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: Area {sid} doesn't have side {(char) ('A' + side)}.");
                        }

                        LevelData level =
                            mode.MapData.Levels.FirstOrDefault(lvl => lvl.Name == levelName) ??
                            mode.MapData.Levels.FirstOrDefault(lvl => lvl.Name == "lvl_" + levelName);
                        if (level == null) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: Area {sid} side {(char) ('A' + side)} doesn't have level {levelName}");
                        }

                        if (session == null ||
                            session.Area.GetSID() != sid ||
                            session.Area.Mode != (AreaMode) side ||
                            data["forcenew"]?.ToLowerInvariant() == "true"
                        ) {
                            session = new Session(area.ToKey(), level.Name);
                        }
                        session.Level = level.Name;

                        float x, y;
                        if (float.TryParse(data["x"], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                            float.TryParse(data["y"], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) {
                            session.RespawnPoint = new Vector2(x, y);
                        } else {
                            session.RespawnPoint = null;
                        }

                        Engine.Scene = new LevelLoader(session, session.RespawnPoint);
                        Write(c, "OK");
                    }
                },

            };

            #endregion

            #region Default RCEndPoint Handler Helpers

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool SetForegroundWindow(IntPtr hWnd);

            private static Type SDL = typeof(Game).Assembly.GetType("SDL2.SDL");
            private static FastReflectionDelegate SDL_RaiseWindow = SDL?.GetMethod("SDL_RaiseWindow")?.GetFastDelegate();

            #endregion

        }
    }
}
