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

namespace Celeste.Mod.Core {
    // Note: If SettingName isn't given, the value defaults to modoptions_[typename without settings]_title
    [SettingName("modoptions_coremodule_title")]
    public class CoreModuleSettings : EverestModuleSettings {

        // Note: If SettingName isn't given, the values default to modoptions_[typename without settings]_[propname]

        // Example runtime setting that only shows up in the menu, not the settings file.
        // [SettingName("modoptions_coremodule_launchindebugmode")]
        [YamlIgnore]
        public bool DebugMode {
            get {
                return Settings.Instance.LaunchInDebugMode;
            }
            set {
                if (Settings.Instance.LaunchInDebugMode == value)
                    return;
                Settings.Instance.LaunchInDebugMode = value;

                if (value) {
                    Celeste.PlayMode = Celeste.PlayModes.Debug;
                    Engine.Commands.Enabled = true;

                } else {
                    Celeste.PlayMode = Celeste.PlayModes.Normal;
                    Engine.Commands.Enabled = false;
                }

                ((patch_OuiMainMenu) (Engine.Scene as Overworld)?.GetUI<OuiMainMenu>())?.RebuildMainAndTitle();
            }
        }

        [YamlIgnore]
        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        public bool LaunchWithFMODLiveUpdate {
            get {
                return Settings.Instance.LaunchWithFMODLiveUpdate;
            }
            set {
                Settings.Instance.LaunchWithFMODLiveUpdate = value;
            }
        }

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        public bool LaunchWithoutIntro { get; set; } = false;

        [SettingInGame(false)]
        public bool ShowModOptionsInGame { get; set; } = true;

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
        public bool DisableAntiSoftlock { get; set; } = false;

        public string InputGui { get; set; } = "";

        private string _MainMenuMode = "";
        public string MainMenuMode {
            get => _MainMenuMode;
            set {
                _MainMenuMode = value;
                ((patch_OuiMainMenu) (Engine.Scene as Overworld)?.GetUI<OuiMainMenu>())?.RebuildMainAndTitle();
            }
        }

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
}
