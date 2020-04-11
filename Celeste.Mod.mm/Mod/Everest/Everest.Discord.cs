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
using System.Threading;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Discord {

            private static DiscordRpc.EventHandlers DiscordHandlers = new DiscordRpc.EventHandlers();
            public static readonly DiscordRpc.RichPresence DiscordPresence = new DiscordRpc.RichPresence();

            private static Thread Worker;
            private static readonly Queue<Action> Queue = new Queue<Action>();
            private static CancellationTokenSource WaitTokenSource;

            public static void Initialize() {
                Worker = new Thread(WorkerLoop);
                Worker.Name = "Everest Discord Worker";
                Worker.Priority = ThreadPriority.Lowest;
                Worker.IsBackground = true;
                Worker.Start();

                WaitTokenSource = new CancellationTokenSource();

                Events.Celeste.OnExiting += OnGameExit;

                Events.MainMenu.OnCreateButtons += OnMainMenu;
                Events.Level.OnLoadLevel += OnLoadLevel;
                Events.Level.OnExit += OnLevelExit;
            }

            private static void WorkerLoop() {
                string lib = null;
                if (!string.IsNullOrEmpty(CoreModule.Settings.DiscordLib))
                    lib = CoreModule.Settings.DiscordLib;
                else if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    lib = "discord-rpc.dll";
                else if (Environment.OSVersion.Platform == PlatformID.MacOSX) {
                    lib = "libdiscord-rpc.dylib";
                    // FIXME: macOS doesn't see libdiscord-rpc.dylib wherever Celeste.exe is.
                } else if (Environment.OSVersion.Platform == PlatformID.Unix)
                    lib = "libdiscord-rpc.so";

                if (!string.IsNullOrEmpty(lib))
                    DynDll.Mappings["discord-rpc"] = new DynDllMapping() {
                        ResolveAs = lib
                    };

                string discordID = "430794114037055489";
                if (!string.IsNullOrEmpty(CoreModule.Settings.DiscordID))
                    discordID = CoreModule.Settings.DiscordID;

                try {
                    typeof(DiscordRpc).ResolveDynDllImports();
                } catch {
                }
                if (DiscordRpc.Initialize == null) {
                    Logger.Log(LogLevel.Info, "discord", "Discord_Initialize not found - skipping Discord Rich Presence.");
                    return;
                }

                Logger.Log(LogLevel.Verbose, "discord", $"Discord_Initialize found - initializing Discord Rich Presence, app ID {discordID}");

                DiscordHandlers.readyCallback += OnDiscordReady;
                DiscordHandlers.disconnectedCallback += OnDiscordDisconnect;
                DiscordHandlers.errorCallback += OnDiscordError;

                DiscordRpc.Initialize.Invoke(discordID, ref DiscordHandlers, true, "504230");
                DiscordRpc.UpdatePresence(DiscordPresence);

                while (Worker != null) {
                    while (WaitTokenSource == null)
                        continue;
                    try {
                        WaitTokenSource.Token.WaitHandle.WaitOne();
                    } catch (OperationCanceledException) {
                    } catch (ObjectDisposedException) {
                    }

                    lock (Queue) {
                        if (Queue.Count > 0)
                            Queue.Dequeue()?.Invoke();
                    }
                }

                DiscordRpc.Shutdown();
            }

            private static void OnDiscordReady(ref DiscordRpc.DiscordUser user) {
                Logger.Log(LogLevel.Info, "discord", $"ready - connected to {user.username}#{user.discriminator} ({user.userId})");
            }
            private static void OnDiscordDisconnect(int code, string message) {
                Logger.Log(LogLevel.Warn, "discord", $"disconnected - {code}: {message}");
            }
            private static void OnDiscordError(int code, string message) {
                Logger.Log(LogLevel.Error, "discord", $"error - {code}: {message}");
            }

            private static void OnGameExit() {
                Worker = null;
                WaitTokenSource?.Cancel();
                WaitTokenSource?.Dispose();
                WaitTokenSource = null;
            }

            private static void OnMainMenu(OuiMainMenu menu, List<MenuButton> buttons) {
                UpdateText(CoreModule.Settings.DiscordTextInMenu);
            }
            private static void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
                lock (DiscordPresence) {
                    if (DiscordPresence.startTimestamp == 0)
                        DiscordPresence.startTimestamp = DateTimeToDiscordTime(DateTime.UtcNow);
                    DiscordPresence.endTimestamp = 0;

                    UpdateText(CoreModule.Settings.DiscordTextInGame, CoreModule.Settings.DiscordSubtextInGame, level.Session);
                }
            }
            private static void OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
                lock (DiscordPresence) {
                    DiscordPresence.startTimestamp = 0;
                    DiscordPresence.endTimestamp = 0;

                    UpdateText(CoreModule.Settings.DiscordTextInMenu);
                }
            }

            internal static void OnStrawberryCollect() {
                UpdateText(CoreModule.Settings.DiscordTextInGame, CoreModule.Settings.DiscordSubtextInGame);
            }

            private static string FillText(string text, Session session, string area) {
                if (text == null || session == null)
                    return text;
                return text
                    .Replace("((area))", area)
                    .Replace("((side))", ((char) ('A' + (int) session.Area.Mode)).ToString())
                    .Replace("((deaths))", session.Deaths.ToString())
                    .Replace("((strawberries))", session.Strawberries.Count.ToString())
                    .Replace("((room))", session.LevelData.Name)
                    .Replace("((chapternumber))", session.Area.ChapterIndex.ToString())
                ;
            }

            public static long DateTimeToDiscordTime(DateTime time) {
                return (long) Math.Floor((time.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            }

            public static void UpdateText(string details, string state = null, Session session = null) {
                Language language = null;
                if (!(Dialog.Languages?.TryGetValue("english", out language) ?? false))
                    language = null;

                if (session == null)
                    session = (Engine.Scene as Level)?.Session;

                string area = "";

                if (session != null) {
                    area = AreaData.Get(session).Name;
                    area = area?.DialogCleanOrNull(language) ?? area;
                }

                lock (DiscordPresence) {
                    DiscordPresence.details = FillText(details, session, area);
                    DiscordPresence.state = FillText(state, session, area);
                }

                if (Worker == null)
                    return;
                lock (Queue) {
                    Queue.Enqueue(UpdatePresence);
                }
                WaitTokenSource.Cancel();
                WaitTokenSource.Dispose();
                WaitTokenSource = new CancellationTokenSource();
            }

            private static readonly Action UpdatePresence = () => {
                lock (DiscordPresence) {
                    DiscordRpc.UpdatePresence(DiscordPresence);
                }
            };

        }
    }
}
