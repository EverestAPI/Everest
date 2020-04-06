using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using MonoMod.Utils;
using MonoMod;

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

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool NonThreadedGL { get; set; } = false;

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
        public bool AutoUpdateModsOnStartup { get; set; } = false;

        [SettingIgnore]
        public int DebugRCPort { get; set; } = 32270;

        [SettingIgnore]
        public string DiscordLib { get; set; } = "";
        [SettingIgnore]
        public string DiscordID { get; set; } = "";
        [SettingIgnore]
        public string DiscordTextInMenu { get; set; } = "📋 Menu";
        [SettingIgnore]
        public string DiscordTextInGame { get; set; } = "🗻 ((area)) 📼 ((side))";
        [SettingIgnore]
        public string DiscordSubtextInGame { get; set; } = "((deaths)) x 💀 | ((strawberries)) x 🍓";

        [SettingIgnore]
        public int? QuickRestart { get; set; }

        [SettingIgnore]
        public bool ShowManualTextOnDebugMap { get; set; } = true;

        [SettingIgnore]
        public string CurrentVersion { get; set; }

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
            List<string> inputGuiPrefixes = new List<string>();
            inputGuiPrefixes.Add(""); // Auto
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

        public enum VanillaTristate {
            Never,
            Everest,
            Always
        }

    }
}
