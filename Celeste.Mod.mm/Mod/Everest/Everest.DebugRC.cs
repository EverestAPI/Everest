using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Celeste.Mod {
    public class RCEndPoint {
        public string Path;
        public string PathHelp;
        public string PathExample;
        public string Name;
        public string InfoHTML;

        public Action<HttpListenerContext> Handle;
    }

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

                try {
                    Listener = new HttpListener();
                    Listener.Prefixes.Add($"http://localhost:{CoreModule.Settings.DebugRCPort}/");
                    Listener.Start();
                } catch (Exception e) {
                    Logger.LogDetailed(e);
                    try {
                        Listener?.Stop();
                    } catch { }
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ => {
                    Logger.Info("debugrc", $"Started DebugRC thread, available via http://localhost:{CoreModule.Settings.DebugRCPort}/");
                    try {
                        while (Listener.IsListening) {
                            ThreadPool.QueueUserWorkItem(c => {
                                HttpListenerContext context = c as HttpListenerContext;
                                try {
                                    using (context.Request.InputStream)
                                    using (context.Response) {
                                        HandleRequest(context);
                                    }
                                } catch (ThreadAbortException) {
                                    throw;
                                } catch (ThreadInterruptedException) {
                                    throw;
                                } catch (Exception e) {
                                    Logger.Error("debugrc", $"DebugRC failed responding: {e}");
                                }
                            }, Listener.GetContext());
                        }
                    } catch (ThreadAbortException) {
                        throw;
                    } catch (ThreadInterruptedException) {
                        throw;
                    } catch (HttpListenerException e) {
                        // 500 = Listener closed.
                        // 995 = I/O abort due to thread abort or application shutdown.
                        if (e.ErrorCode != 500 &&
                            e.ErrorCode != 995) {
                            Logger.Error("debugrc", $"DebugRC failed listening ({e.ErrorCode}): {e}");
                        }
                    } catch (Exception e) {
                        Logger.Error("debugrc", $"DebugRC failed listening: {e}");
                    }
                });
            }

            public static void Shutdown() {
                Listener?.Abort();
                Listener = null;
            }

            private static void HandleRequest(HttpListenerContext c) {
                Logger.Verbose("debugrc", $"Requested: {c.Request.RawUrl}");

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
                    Path = "/hotswap",
                    Name = "Hotswap",
                    InfoHTML = "Restart the entire game AppDomain real quick.",
                    Handle = c => {
                        Write(c, "OK.");
                        QuickFullRestart();
                    }
                },

                new RCEndPoint {
                    Path = "/coldswap",
                    Name = "Coldswap",
                    InfoHTML = "Restart the entire game process real quick.",
                    Handle = c => {
                        Write(c, "OK.");
                        SlowFullRestart();
                    }
                },

                new RCEndPoint {
                    Path = "/list",
                    PathHelp = "/list?type={mods|modcontent} (Example: ?type=mods)",
                    PathExample = "/list?type=mods",
                    Name = "List Info",
                    InfoHTML = "List some basic info.",
                    Handle = c => {
                        StringBuilder builder = new StringBuilder();

                        NameValueCollection data = ParseQueryString(c.Request.RawUrl);

                        switch (data["type"]) {
                            case "mods":
                                lock (_Modules) {
                                    foreach (EverestModule module in _Modules) {
                                        builder.AppendLine(module.Metadata.Name);
                                    }
                                }
                                break;

                            case "modcontent":
                                lock (Content.Map) {
                                    foreach (ModAsset asset in Content.Map.Values) {
                                        builder.AppendLine(asset.PathVirtual);
                                    }
                                }
                                break;

                            default:
                                c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                                builder.Append($"ERROR: Invalid type: {data["type"] ?? "NULL"}");
                                break;
                        }

                        Write(c, builder.ToString());
                    }
                },

                new RCEndPoint {
                    Path = "/session",
                    Name = "Session Info",
                    InfoHTML = "Basic player session info.",
                    Handle = c => {
                         Write(c, YamlHelper.Serializer.Serialize(new SessionInfo(Engine.Scene as Level)));
                    }
                },

                new RCEndPoint {
                    Path = "/focus",
                    Name = "Focus Game",
                    InfoHTML = "Refocus the game window. Doesn't work on Windows 10.",
                    Handle = c => {
                        if (SDL_RaiseWindow != null)
                            SDL_RaiseWindow(Celeste.Instance.Window.Handle);
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
                        if (!(Engine.Scene is Level level)) {
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

                        AreaData area = patch_AreaData.Get(sid);
                        if (area == null) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: Chapter not found: {sid}");
                            return;
                        }

                        string sideStr = data["side"];
                        if (string.IsNullOrEmpty(sideStr)) {
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
                            return;
                        }

                        area.Mode[side].MapData.Reload();

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

                        float x = 0;
                        float y = 0;
                        bool hasXY =
                            float.TryParse(data["x"], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                            float.TryParse(data["y"], NumberStyles.Float, CultureInfo.InvariantCulture, out y);

                        string levelName = data["level"];
                        if (levelName != null && levelName.StartsWith("lvl_"))
                            levelName = levelName.Substring(4);

                        // Special case: Update X and Y in existing session.
                        if (string.IsNullOrEmpty(data["area"]) &&
                            string.IsNullOrEmpty(data["side"]) &&
                            hasXY
                        ) {
                            if (session == null) {
                                c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                                Write(c, $"ERROR: In-level (x-y-only) tp outside of level.");
                                return;
                            }

                            Player player = (Engine.Scene as Level)?.Tracker.GetEntity<Player>();
                            bool reload = false;
                            if (!string.IsNullOrEmpty(levelName) && session.Level != levelName) {
                                if (session.MapData.Levels.FirstOrDefault(lvl => lvl.Name == levelName) == null) {
                                    c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                                    Write(c, $"ERROR: Area {session.Area.GetSID()} side {(char) ('A' + (int) session.Area.Mode)} doesn't have level {levelName}");
                                    return;
                                }

                                session.Level = levelName;
                                reload = true;
                            }

                            Vector2 pos = session.LevelData.Position + new Vector2(x, y);
                            session.RespawnPoint = pos;
                            player.Position = pos;
                            if (reload) {
                                Engine.Scene.Paused = true;
                                Engine.Scene = new LevelLoader(session, pos);
                            }
                            Write(c, "OK");
                            return;
                        }

                        string sid = data["area"];
                        if (string.IsNullOrEmpty(sid))
                            sid = session.Area.GetSID();
                        if (string.IsNullOrEmpty(sid)) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: No SID given.");
                            return;
                        }

                        patch_AreaData area = patch_AreaData.Get(sid);
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

                        if (string.IsNullOrEmpty(levelName)) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: No level given.");
                            return;
                        }

                        ModeProperties mode = area.Mode[side];
                        if (mode == null) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: Area {sid} doesn't have side {(char) ('A' + side)}.");
                            return;
                        }

                        LevelData level = mode.MapData.Levels.FirstOrDefault(lvl => lvl.Name == levelName);
                        if (level == null) {
                            c.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            Write(c, $"ERROR: Area {sid} side {(char) ('A' + side)} doesn't have level {levelName}");
                            return;
                        }

                        if (session == null ||
                            session.Area.GetSID() != sid ||
                            session.Area.Mode != (AreaMode) side ||
                            data["forcenew"]?.ToLowerInvariant() == "true"
                        ) {
                            session = new Session(area.ToKey((AreaMode) side), level.Name);
                        }
                        session.Level = levelName;

                        if (hasXY) {
                            session.RespawnPoint = level.Position + new Vector2(x, y);
                        } else {
                            session.RespawnPoint = null;
                        }

                        Engine.Scene = new LevelLoader(session, session.RespawnPoint);
                        Write(c, "OK");
                    }
                },
                
                new RCEndPoint {
                    Path = "/console",
                    Name = "Console",
                    PathHelp = "/console?command={*|COMMAND} (Example: ?command=berries)",
                    PathExample = "/console?command=berries",
                    InfoHTML = "Execute a console command and show the output. If no command is given, list the available commands.",
                    Handle = c => {
                        NameValueCollection data = ParseQueryString(c.Request.RawUrl);
                        
                        string rawCommand = WebUtility.UrlDecode(data["command"]);
                        if (string.IsNullOrWhiteSpace(rawCommand) || string.IsNullOrWhiteSpace(rawCommand.Replace(",", ""))) {
                            StringBuilder commandList = new StringBuilder();
                            WriteHTMLStart(c, commandList);
                            commandList.AppendLine(@"<ul>");
                            commandList.AppendLine(@"<h2>Commands</h2>");
                            foreach (var command in ((Monocle.patch_Commands) Engine.Commands).GetCommands().OrderBy(comm => comm.Name))
                            {
                                commandList.AppendLine(@"<li>");
                                commandList.AppendLine($@"<h3>{command.Name}</h3>");
                                commandList.AppendLine(@"<p>");
                                if (string.IsNullOrEmpty(command.Usage)) {
                                    commandList.AppendLine($@"<a href=""{Listener.Prefixes.First()}console?command={command.Name}""><code>/console?command={command.Name}</code></a>");
                                } else {
                                    commandList.AppendLine($@"<code>Usage: {command.Usage}</code>");
                                }
                                commandList.AppendLine(@"<br>");
                                commandList.AppendLine(command.Help);
                                commandList.AppendLine(@"</p>");
                                commandList.AppendLine(@"</li>");
                            }
                            commandList.AppendLine(@"</ul>");
                            WriteHTMLEnd(c, commandList);
                            Write(c, commandList.ToString());
                        } else {
                            string[] commandAndArgs = rawCommand.Split(new[] {' ', ','}, StringSplitOptions.RemoveEmptyEntries);
                            string[] args = new string[commandAndArgs.Length - 1];
                            Array.Copy(commandAndArgs, 1, args, 0, args.Length);

                            StringBuilder output = new StringBuilder();
                            MainThreadHelper.Schedule(() => { // prevent interfering with commands run from ingame console
                                try {
                                    ((Monocle.patch_Commands) Engine.Commands).debugRClog = output;
                                    Engine.Commands.ExecuteCommand(commandAndArgs[0].ToLower(), args);
                                } finally {
                                    ((Monocle.patch_Commands) Engine.Commands).debugRClog = null;
                                }
                            }).AsTask().Wait(); // wait for command to finish before writing output
                            Write(c, output.ToString());
                        }
                    }
                },

            };

            #endregion

            #region Default RCEndPoint Handler Helpers

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool SetForegroundWindow(IntPtr hWnd);

            private static Type SDL = typeof(Game).Assembly.GetType("SDL2.SDL");
            private static Action<IntPtr> SDL_RaiseWindow = SDL?.GetMethod("SDL_RaiseWindow")?.CreateDelegate<Action<IntPtr>>(null);

            private class SessionInfo {

                public string Area { get; set; }
                public char Side { get; set; } = '?';
                public string Level { get; set; }

                public string MapBin { get; set; }

                public float X { get; set; }
                public float Y { get; set; }

                public string TP => string.IsNullOrEmpty(Area) ? "" : $"/tp?area={Area}&side={Side}&level={Level}&x={X.ToString(CultureInfo.InvariantCulture)}&y={Y.ToString(CultureInfo.InvariantCulture)}";

                public SessionInfo(Level level) {
                    if (level?.Session == null)
                        return;

                    Area = level.Session.Area.GetSID();
                    Side = (char) ('A' + level.Session.Area.Mode);
                    Level = level.Session.Level;

                    MapBin = level.Session.MapData.Filename;

                    Vector2 pos = level.Tracker.GetEntity<Player>()?.Position ?? level.Session.RespawnPoint ?? Vector2.Zero;
                    pos -= level.Session.LevelData.Position;
                    X = pos.X;
                    Y = pos.Y;
                }

            }

            #endregion

        }
    }
}
