using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Core {
    // Note: If SettingName isn't given, the value defaults to modoptions_[typename without settings]_title
    [SettingName("modoptions_coremodule_title")]
    public class CoreModuleSettings : EverestModuleSettings {

        // Note: If SettingName isn't given, the values default to modoptions_[typename without settings]_[propname]

        // Example runtime setting that only shows up in the menu, not the settings file.
        // [SettingName("modoptions_coremodule_launchindebugmode")]
        [YamlIgnore]
        public VanillaTristate DebugMode {
            get {
                return
                    Settings.Instance.LaunchInDebugMode ? VanillaTristate.Always :
                    DebugModeInEverest ? VanillaTristate.Everest :
                    VanillaTristate.Never;
            }
            set {
                switch (value) {
                    case VanillaTristate.Never:
                    default:
                        DebugModeInEverest = false;
                        Settings.Instance.LaunchInDebugMode = false;
                        break;

                    case VanillaTristate.Everest:
                        DebugModeInEverest = true;
                        Settings.Instance.LaunchInDebugMode = false;
                        break;

                    case VanillaTristate.Always:
                        DebugModeInEverest = true;
                        Settings.Instance.LaunchInDebugMode = true;
                        break;
                }
            }
        }

        // before being a tri-state, DebugMode was a boolean. set up a property for compatibility.
        [YamlIgnore]
        [SettingIgnore]
        [Obsolete("Use DebugMode instead.")]
        public bool DebugModeOld {
            [MonoModLinkFrom("System.Boolean Celeste.Mod.Core.CoreModuleSettings::get_DebugMode()")]
            get => DebugMode != VanillaTristate.Never;

            [MonoModLinkFrom("System.Void Celeste.Mod.Core.CoreModuleSettings::set_DebugMode(System.Boolean)")]
            set => DebugMode = value ? VanillaTristate.Always : VanillaTristate.Never;
        }

        private bool _DebugModeInEverest;
        [SettingIgnore]
        public bool DebugModeInEverest {
            get => _DebugModeInEverest || Settings.Instance.LaunchInDebugMode; // Everest debug mode must be enabled when vanilla debug mode is.
            set {
                _DebugModeInEverest = value;

                if (Celeste.PlayMode == Celeste.PlayModes.Debug == value)
                    return;

                if (value) {
                    Celeste.PlayMode = Celeste.PlayModes.Debug;
                    if (Engine.Commands != null)
                        Engine.Commands.Enabled = true;

                } else {
                    Celeste.PlayMode = Celeste.PlayModes.Normal;
                    if (Engine.Commands != null)
                        Engine.Commands.Enabled = false;
                }

                ((patch_OuiMainMenu) (Engine.Scene as Overworld)?.GetUI<OuiMainMenu>())?.RebuildMainAndTitle();
            }
        }

        [YamlIgnore]
        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        public VanillaTristate LaunchWithFMODLiveUpdate {
            get {
                return
                    Settings.Instance.LaunchWithFMODLiveUpdate ? VanillaTristate.Always :
                    LaunchWithFMODLiveUpdateInEverest ? VanillaTristate.Everest :
                    VanillaTristate.Never;
            }
            set {
                switch (value) {
                    case VanillaTristate.Never:
                    default:
                        LaunchWithFMODLiveUpdateInEverest = false;
                        Settings.Instance.LaunchWithFMODLiveUpdate = false;
                        break;

                    case VanillaTristate.Everest:
                        LaunchWithFMODLiveUpdateInEverest = true;
                        Settings.Instance.LaunchWithFMODLiveUpdate = false;
                        break;

                    case VanillaTristate.Always:
                        LaunchWithFMODLiveUpdateInEverest = true;
                        Settings.Instance.LaunchWithFMODLiveUpdate = true;
                        break;
                }
            }
        }

        [SettingIgnore]
        public bool LaunchWithFMODLiveUpdateInEverest { get; set; }

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        public bool LaunchWithoutIntro { get; set; } = false;

        [SettingInGame(false)]
        public bool ShowModOptionsInGame { get; set; } = true;

        [YamlIgnore]
        [SettingInGame(false)]
        public bool ShowEverestTitleScreen {
            get {
                if (DateTime.Now.Month == 4 && DateTime.Now.Day == 1)
                    return TitleScreenType != "vanilla";
                return TitleScreenType == "everest";
            }
            set {
                TitleScreenType = value ? "everest" : "vanilla";
            }
        }

        [SettingIgnore]
        public string TitleScreenType { get; set; }

        [SettingIgnore]
        public bool LazyLoading_Yes_I_Know_This_Can_Cause_Bugs { get; set; } = false;
        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        [YamlIgnore]
        public bool LazyLoading {
            get => LazyLoading_Yes_I_Know_This_Can_Cause_Bugs;
            set => LazyLoading_Yes_I_Know_This_Can_Cause_Bugs = value;
        }

        [SettingIgnore]
        public string DefaultStartingLevelSet { get; set; } = "Celeste";

        [SettingIgnore]
        public int LogHistoryCountToKeep { get; set; } = 3;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool? ThreadedGL { get; set; } = null;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool? FastTextureLoading { get; set; } = null;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public float FastTextureLoadingMaxMB { get; set; } = 0;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool UnpackFMODBanks { get; set; } = true;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool UnloadUnusedAudio { get; set; } = true;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool DisableAntiSoftlock { get; set; } = false;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool? MultithreadedGC { get; set; } = null;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool RestartAppDomain_WIP { get; set; } = false;

        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public int? MaxSaveSlots { get; set; } = null;

        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public int ExtraCommandHistoryLines { get; set; } = 50;

        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool? SaveDataFlush { get; set; } = null;

        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool? WhitelistFullOverride { get; set; } = null;

        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool OpenErrorLogOnCrash { get; set; } = true;

        public string InputGui { get; set; } = "";

        private string _MainMenuMode = "";
        public string MainMenuMode {
            get => _MainMenuMode;
            set {
                string originalValue = _MainMenuMode;
                _MainMenuMode = value;
                if (value != originalValue) {
                    // the main menu mode was changed; rebuild the main menu
                    ((patch_OuiMainMenu) (Engine.Scene as Overworld)?.GetUI<OuiMainMenu>())?.RebuildMainAndTitle();
                }
            }
        }

        [SettingInGame(false)]
        public bool UseKeyboardForTextInput { get; set; } = true;

        [SettingInGame(false)]
        public bool AutoUpdateModsOnStartup { get; set; } = false;


        private bool _WarnOnEverestYamlErrors = false;
        [SettingSubText("MODOPTIONS_COREMODULE_WARNONEVERESTYAMLERRORS_DESC")]
        [SettingInGame(false)]
        public bool WarnOnEverestYamlErrors {
            get => _WarnOnEverestYamlErrors;
            set {
                _WarnOnEverestYamlErrors = value;

                // rebuild the main menu to make sure we show/hide the yaml error notice.
                ((patch_OuiMainMenu) (Engine.Scene as Overworld)?.GetUI<OuiMainMenu>())?.RebuildMainAndTitle();
            }
        }

        public bool DiscordRichPresence { get; set; } = true;

        [SettingIgnore]
        public bool DiscordShowIcon { get; set; } = true;
        [SettingIgnore]
        public bool DiscordShowMap { get; set; } = true;
        [SettingIgnore]
        public bool DiscordShowSide { get; set; } = true;
        [SettingIgnore]
        public bool DiscordShowRoom { get; set; } = false;
        [SettingIgnore]
        public bool DiscordShowBerries { get; set; } = true;
        [SettingIgnore]
        public bool DiscordShowDeaths { get; set; } = true;

        [SettingIgnore]
        public int DebugRCPort { get; set; } = 32270;

        [SettingIgnore]
        public int? QuickRestart { get; set; }

        [SettingIgnore]
        public bool ShowManualTextOnDebugMap { get; set; } = true;

        [SettingIgnore]
        public bool CodeReload_WIP { get; set; } = false;

        // TODO: Once CodeReload is no longer WIP, remove this and rename ^ to non-WIP.
        [SettingIgnore]
        [YamlIgnore]
        public bool CodeReload {
            get => CodeReload_WIP;
            set => CodeReload_WIP = value;
        }

        [SettingIgnore]
        public string CurrentVersion { get; set; }

        [SettingIgnore]
        public string CurrentBranch { get; set; }

        [SettingIgnore]
        public Dictionary<string, LogLevel> LogLevels { get; set; } = new Dictionary<string, LogLevel>();

        [SettingSubHeader("MODOPTIONS_COREMODULE_MENUNAV_SUBHEADER")]
        [SettingInGame(false)]
        public ButtonBinding MenuPageUp { get; set; }

        [SettingInGame(false)]
        public ButtonBinding MenuPageDown { get; set; }

        [SettingSubHeader("MODOPTIONS_COREMODULE_DEBUGMODE_SUBHEADER")]
        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.OemPeriod)]
        public ButtonBinding DebugConsole { get; set; }

        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.F6)]
        public ButtonBinding DebugMap { get; set; }

        [SettingSubHeader("MODOPTIONS_COREMODULE_MOUNTAINCAM_SUBHEADER")]
        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.W)]
        public ButtonBinding CameraForward { get; set; }

        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.S)]
        public ButtonBinding CameraBackward { get; set; }

        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.D)]
        public ButtonBinding CameraRight { get; set; }

        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.A)]
        public ButtonBinding CameraLeft { get; set; }

        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.Q)]
        public ButtonBinding CameraUp { get; set; }

        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.Z)]
        public ButtonBinding CameraDown { get; set; }

        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.LeftShift)]
        public ButtonBinding CameraSlow { get; set; }

        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.P)]
        public ButtonBinding CameraPrint { get; set; }

        [SettingInGame(false)]
        [DefaultButtonBinding(0, Keys.Space)]
        public ButtonBinding ToggleMountainFreeCam { get; set; }

        /*
        [SettingRange(0, 10)]
        public int ExampleSlider { get; set; } = 5;

        [SettingRange(0, 10)]
        [SettingInGame(false)]
        public int ExampleMainMenuSlider { get; set; } = 5;

        [SettingRange(0, 10)]
        [SettingInGame(true)]
        public int ExampleInGameSlider { get; set; } = 5;
        */

        public void CreateInputGuiEntry(TextMenu menu, bool inGame) {
            // Get all Input GUI prefixes and add a slider for switching between them.
            List<string> inputGuiPrefixes = new List<string> {
                "" // Auto
            };
            foreach (KeyValuePair<string, MTexture> kvp in GFX.Gui.GetTextures()) {
                string path = kvp.Key;
                if (!path.StartsWith("controls/"))
                    continue;
                path = path.Substring(9);
                int indexOfSlash = path.IndexOf('/');
                if (indexOfSlash == -1)
                    continue;
                path = path.Substring(0, indexOfSlash);
                if (!inputGuiPrefixes.Contains(path))
                    inputGuiPrefixes.Add(path);
            }

            menu.Add(
                new TextMenu.Slider(Dialog.Clean("modoptions_coremodule_inputgui"), i => {
                    string inputGuiPrefix = inputGuiPrefixes[i];
                    string fullName = $"modoptions_coremodule_inputgui_{inputGuiPrefix.ToLowerInvariant()}";
                    return fullName.DialogCleanOrNull() ?? inputGuiPrefix.ToUpperInvariant();
                }, 0, inputGuiPrefixes.Count - 1, Math.Max(0, inputGuiPrefixes.IndexOf(InputGui)))
                .Change(i => {
                    InputGui = inputGuiPrefixes[i];
                    Input.OverrideInputPrefix = inputGuiPrefixes[i];
                })
            );
        }

        public void CreateMainMenuModeEntry(TextMenu menu, bool inGame) {
            if (!inGame) {
                // TODO: Let mods register custom main menu modes?
                List<string> types = new List<string>() {
                    "",
                    "rows"
                };

                menu.Add(
                    new TextMenu.Slider(Dialog.Clean("modoptions_coremodule_mainmenumode"), i => {
                        string prefix = types[i];
                        string fullName = $"modoptions_coremodule_mainmenumode_{prefix.ToLowerInvariant()}";
                        return fullName.DialogCleanOrNull() ?? prefix.ToUpperInvariant();
                    }, 0, types.Count - 1, Math.Max(0, types.IndexOf(MainMenuMode)))
                    .Change(i => {
                        MainMenuMode = types[i];
                    })
                );
            }
        }

        public void CreateDiscordRichPresenceEntry(TextMenu menu, bool inGame) {
            Session session = (Engine.Scene as Level)?.Session;

            TextMenu.Option<bool> showSide = new TextMenu.OnOff(Dialog.Clean("modoptions_coremodule_discordshowside"), DiscordShowSide)
                .Change(value => {
                    DiscordShowSide = value;
                    Everest.DiscordSDK.Instance?.UpdatePresence(session);
                });

            TextMenu.Option<bool> showRoom = new TextMenu.OnOff(Dialog.Clean("modoptions_coremodule_discordshowroom"), DiscordShowRoom)
                .Change(value => {
                    DiscordShowRoom = value;
                    Everest.DiscordSDK.Instance?.UpdatePresence(session);
                });

            TextMenu.Item showMap = new TextMenu.OnOff(Dialog.Clean("modoptions_coremodule_discordshowmap"), DiscordShowMap)
                .Change(value => {
                    DiscordShowMap = value;
                    Everest.DiscordSDK.Instance?.UpdatePresence(session);

                    showSide.Disabled = !DiscordShowMap;
                    showRoom.Disabled = !DiscordShowMap;

                    if (!DiscordShowMap) {
                        showSide.Index = 0;
                        showRoom.Index = 0;
                        DiscordShowSide = false;
                        DiscordShowRoom = false;
                    }
                });

            TextMenu.Item showIcon = new TextMenu.OnOff(Dialog.Clean("modoptions_coremodule_discordshowicon"), DiscordShowIcon)
                .Change(value => {
                    DiscordShowIcon = value;
                    Everest.DiscordSDK.Instance?.UpdatePresence(session);
                });

            TextMenu.Item showDeaths = new TextMenu.OnOff(Dialog.Clean("modoptions_coremodule_discordshowdeaths"), DiscordShowDeaths)
                .Change(value => {
                    DiscordShowDeaths = value;
                    Everest.DiscordSDK.Instance?.UpdatePresence(session);
                });

            TextMenu.Item showBerries = new TextMenu.OnOff(Dialog.Clean("modoptions_coremodule_discordshowberries"), DiscordShowBerries)
                .Change(value => {
                    DiscordShowBerries = value;
                    Everest.DiscordSDK.Instance?.UpdatePresence(session);
                });

            TextMenuExt.SubMenu submenu = new TextMenuExt.SubMenu(Dialog.Clean("modoptions_coremodule_discordrichpresenceoptions"), false)
                .Add(showIcon)
                .Add(showMap)
                .Add(showSide)
                .Add(showRoom)
                .Add(showDeaths)
                .Add(showBerries);

            TextMenuExt.EaseInSubHeaderExt failureWarning = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean("modoptions_coremodule_discordfailed"), false, menu) {
                TextColor = Color.Goldenrod,
                HeightExtra = 0f
            };

            TextMenu.Item masterSwitch = new TextMenu.OnOff(Dialog.Clean("modoptions_coremodule_discordrichpresence"), DiscordRichPresence)
                .Change(value => {
                    DiscordRichPresence = value;
                    if (DiscordRichPresence) {
                        Everest.DiscordSDK.CreateInstance()?.UpdatePresence(session);
                    } else {
                        Everest.DiscordSDK.Instance?.Dispose();
                    }
                    submenu.Disabled = !value;
                    failureWarning.FadeVisible = DiscordRichPresence && Everest.DiscordSDK.Instance == null;
                });

            masterSwitch.OnEnter += delegate {
                failureWarning.FadeVisible = DiscordRichPresence && Everest.DiscordSDK.Instance == null;
            };
            masterSwitch.OnLeave += delegate {
                failureWarning.FadeVisible = false;
            };

            menu.Add(masterSwitch);
            menu.Add(failureWarning);

            submenu.Disabled = !DiscordRichPresence;
            showSide.Disabled = !DiscordShowMap;
            showRoom.Disabled = !DiscordShowMap;

            menu.Add(submenu);
        }

        public enum VanillaTristate {
            Never,
            Everest,
            Always
        }

    }
}
