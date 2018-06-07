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

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Discord {

            private static DiscordRpc.EventHandlers DiscordHandlers = new DiscordRpc.EventHandlers();
            public static DiscordRpc.RichPresence DiscordPresence = new DiscordRpc.RichPresence();

            public static void Initialize() {
                if (!string.IsNullOrEmpty(CoreModule.Settings.DiscordLib))
                    DynDll.DllMap["discord-rpc"] = CoreModule.Settings.DiscordLib;
                else if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    DynDll.DllMap["discord-rpc"] = "discord-rpc.dll";
                else if (Environment.OSVersion.Platform == PlatformID.MacOSX) {
                    DynDll.DllMap["discord-rpc"] = "libdiscord-rpc.dylib";
                    // FIXME: macOS doesn't see libdiscord-rpc.dylib wherever Celeste.exe is.
                } else if (Environment.OSVersion.Platform == PlatformID.Unix)
                    DynDll.DllMap["discord-rpc"] = "libdiscord-rpc.so";

                string discordID = "430794114037055489";
                if (!string.IsNullOrEmpty(CoreModule.Settings.DiscordID))
                    discordID = CoreModule.Settings.DiscordID;

                try {
                    typeof(DiscordRpc).ResolveDynDllImports();
                } catch (EntryPointNotFoundException) {
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

                Events.Celeste.OnExiting += OnGameExit;

                Events.MainMenu.OnCreateButtons += OnMainMenu;
                Events.Level.OnLoadLevel += OnLoadLevel;
                Events.Level.OnExit += OnLevelExit;
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
                if (DiscordRpc.Initialize == null)
                    return;
                DiscordRpc.Shutdown();
            }

            private static void OnMainMenu(OuiMainMenu menu, List<MenuButton> buttons) {
                UpdateText(CoreModule.Settings.DiscordTextInMenu);
            }
            private static void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
                DateTime now = DateTime.UtcNow;
                if (DiscordPresence.startTimestamp == 0)
                    DiscordPresence.startTimestamp = DateTimeToDiscordTime(DateTime.UtcNow);
                DiscordPresence.endTimestamp = 0;

                UpdateText(CoreModule.Settings.DiscordTextInGame, CoreModule.Settings.DiscordSubtextInGame, level.Session);
            }
            private static void OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
                DiscordPresence.startTimestamp = 0;
                DiscordPresence.endTimestamp = 0;

                UpdateText(CoreModule.Settings.DiscordTextInMenu);
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

                DiscordPresence.details = FillText(details, session, area);
                DiscordPresence.state = FillText(state, session, area);

                if (DiscordRpc.Initialize == null)
                    return;

                DiscordRpc.UpdatePresence(DiscordPresence);
            }

        }
    }
}
