using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod {
    public static partial class Everest {
        public class DiscordSDK : GameComponent {
            public static DiscordSDK Instance { get; private set; } = null;

            private Discord.Discord DiscordInstance;

            private const string IconBaseURL = "https://max480-random-stuff.appspot.com/celeste/rich-presence-icons/";

            private Dictionary<string, string> IconURLCache = new Dictionary<string, string>();
            private Discord.Activity NextPresence;
            private bool MustUpdatePresence;

            private long StartTimestamp = 0;

            private static readonly Dictionary<Discord.LogLevel, LogLevel> DiscordToEverestLogLevel = new Dictionary<Discord.LogLevel, LogLevel>() {
                { Discord.LogLevel.Error, LogLevel.Error },
                { Discord.LogLevel.Warn, LogLevel.Warn },
                { Discord.LogLevel.Info, LogLevel.Info },
                { Discord.LogLevel.Debug, LogLevel.Debug }
            };

            public static DiscordSDK CreateInstance() {
                if (Instance != null) {
                    return Instance;
                }

                DiscordSDK sdk = new DiscordSDK(Celeste.Instance);
                if (sdk.DiscordInstance != null) {
                    Instance = sdk;
                }

                return Instance;
            }

            private DiscordSDK(Game game) : base(game) {
                UpdateOrder = -500000;

                Logger.Log(LogLevel.Verbose, "discord-game-sdk", $"Initializing Discord Game SDK...");
                try {
                    DiscordInstance = new Discord.Discord(430794114037055489L, (ulong) Discord.CreateFlags.NoRequireDiscord);
                } catch (Exception e) {
                    Logger.Log(LogLevel.Error, "discord-game-sdk", "Could not initialize Discord Game SDK!");
                    Logger.LogDetailed(e, "discord-game-sdk");
                    return;
                }
                DiscordInstance.SetLogHook(Discord.LogLevel.Debug, LogHandler);

                DiscordInstance.GetUserManager().OnCurrentUserUpdate += () => {
                    Discord.User user = DiscordInstance.GetUserManager().GetCurrentUser();
                    Logger.Log(LogLevel.Verbose, "discord-game-sdk", $"Connected user is {user.Username}#{user.Discriminator}");
                };

                Events.Celeste.OnExiting += OnGameExit;
                Events.MainMenu.OnCreateButtons += OnMainMenu;
                Events.Level.OnLoadLevel += OnLoadLevel;
                Events.Level.OnExit += OnLevelExit;

                Celeste.Instance.Components.Add(this);

                Logger.Log(LogLevel.Debug, "discord-game-sdk", "Game SDK initialized!");
            }

            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);

                Events.Celeste.OnExiting -= OnGameExit;
                Events.MainMenu.OnCreateButtons -= OnMainMenu;
                Events.Level.OnLoadLevel -= OnLoadLevel;
                Events.Level.OnExit -= OnLevelExit;

                DiscordInstance?.Dispose();

                Instance = null;
                Celeste.Instance.Components.Remove(this);

                Logger.Log(LogLevel.Debug, "discord-game-sdk", "Game SDK disposed");
            }

            public override void Update(GameTime gameTime) {
                if (MustUpdatePresence) {
                    Logger.Log(LogLevel.Verbose, "discord-game-sdk", $"Changing activity: state='{NextPresence.State}', " +
                    $"details='{NextPresence.Details}', image='{NextPresence.Assets.LargeImage}', text='{NextPresence.Assets.LargeText}', timestamp='{NextPresence.Timestamps.Start}'");

                    DiscordInstance.GetActivityManager().UpdateActivity(NextPresence, (result) => {
                        if (result == Discord.Result.Ok) {
                            Logger.Log(LogLevel.Verbose, "discord-game-sdk", "Presence changed successfully!");
                        } else {
                            Logger.Log(LogLevel.Warn, "discord-game-sdk", $"Failed to change presence: {result}");
                        }
                    });

                    MustUpdatePresence = false;
                }

                DiscordInstance.RunCallbacks();
            }

            private void LogHandler(Discord.LogLevel level, string message) {
                Logger.Log(DiscordToEverestLogLevel[level], "discord-game-sdk", message);
            }

            private void OnGameExit() {
                Dispose();
            }

            private void OnMainMenu(OuiMainMenu menu, List<MenuButton> buttons) {
                UpdatePresence();
            }

            private void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
                if (StartTimestamp == 0) {
                    StartTimestamp = DateTimeToDiscordTime(DateTime.UtcNow);
                }

                UpdatePresence(level.Session);
            }

            private void OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
                StartTimestamp = 0;
                UpdatePresence();
            }

            internal void UpdatePresence(Session session = null) {
                if (session == null) {
                    NextPresence = new Discord.Activity {
                        Details = "In Menus"
                    };

                    if (CoreModule.Settings.DiscordShowIcon) {
                        NextPresence.Assets = new Discord.ActivityAssets() {
                            LargeImage = IconBaseURL + "everest.png",
                            LargeText = "Everest",
                            SmallImage = IconBaseURL + "celeste.png",
                            SmallText = "Celeste"
                        };
                    }
                } else {
                    Language english = Dialog.Languages["english"];
                    AreaData area = AreaData.Get(session);

                    // the displayed info if "show map" was disabled: just "Playing a map"
                    string mapName = "a map";
                    string fullName = "Everest";
                    string icon = IconBaseURL + "everest.png";
                    string side = "";
                    string room = "";

                    if (CoreModule.Settings.DiscordShowMap) {
                        mapName = area.Name.DialogCleanOrNull(english) ?? area.Name;

                        if (CoreModule.Settings.DiscordShowIcon) {
                            icon = GetMapIconURLCached(area);
                        }

                        if (CoreModule.Settings.DiscordShowSide && area.Mode.Length >= 2 && area.Mode[2] != null) {
                            side = " | " + (char) ('A' + session.Area.Mode) + "-Side";
                        }

                        if (CoreModule.Settings.DiscordShowRoom) {
                            room = " | Room " + session.Level;
                        }

                        if (!IsOnlyMapInLevelSet(area)) {
                            fullName = (area.GetLevelSet().DialogCleanOrNull(english) ?? area.GetLevelSet())
                                + " | " + (session.Area.ChapterIndex >= 0 ? "Chapter " + session.Area.ChapterIndex + " - " : "") + mapName;
                        } else {
                            fullName = mapName;
                        }
                    }

                    string state = "";
                    if (CoreModule.Settings.DiscordShowBerries) {
                        state = Pluralize(session.Strawberries.Count, "berry", "berries");
                    }
                    if (CoreModule.Settings.DiscordShowDeaths) {
                        if (!string.IsNullOrEmpty(state)) {
                            state += " | ";
                        }

                        state += Pluralize(session.Deaths, "death", "deaths");
                    }

                    NextPresence = new Discord.Activity {
                        Details = "Playing " + mapName + side + room,
                        State = state,
                        Timestamps = {
                            Start = StartTimestamp
                        }
                    };

                    if (CoreModule.Settings.DiscordShowIcon) {
                        NextPresence.Assets = new Discord.ActivityAssets() {
                            LargeImage = icon,
                            LargeText = fullName,
                            SmallImage = IconBaseURL + "celeste.png",
                            SmallText = "Celeste"
                        };
                    }
                }

                MustUpdatePresence = true;
            }

            private bool IsOnlyMapInLevelSet(AreaData area) {
                foreach (AreaData otherArea in AreaData.Areas) {
                    if (area.GetLevelSet() == otherArea.GetLevelSet() && area.GetSID() != otherArea.GetSID()) {
                        return false;
                    }
                }
                return true;
            }

            private long DateTimeToDiscordTime(DateTime time) {
                return (long) Math.Floor((time.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            }

            private string GetMapIconURLCached(AreaData areaData) {
                if (IconURLCache.TryGetValue(areaData.Icon, out string url)) {
                    return url;
                }

                url = GetMapIconURL(areaData);
                IconURLCache.Add(areaData.Icon, url);
                return url;
            }

            private string GetMapIconURL(AreaData areaData) {
                if (areaData.Icon == "areas/null" || !Content.Map.TryGetValue("Graphics/Atlases/Gui/" + areaData.Icon, out ModAsset icon)) {
                    if (areaData.Icon.StartsWith("areas/")) {
                        return IconBaseURL + areaData.Icon.Substring(6) + ".png";
                    } else {
                        return IconBaseURL + "null.png";
                    }
                } else {
                    byte[] hash = ChecksumHasher.ComputeHash(icon.Data);
                    string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    return IconBaseURL + hashString + ".png";
                }
            }

            private string Pluralize(int number, string singular, string plural) {
                return number + " " + (number == 1 ? singular : plural);
            }
        }
    }
}
