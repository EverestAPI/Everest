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
        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        public bool LaunchInDebugMode {
            get {
                return Settings.Instance.LaunchInDebugMode;
            }
            set {
                Settings.Instance.LaunchInDebugMode = value;
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

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool LazyLoading { get; set; } = false;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool LQAtlas { get; set; } = false;

        [SettingNeedsRelaunch]
        [SettingInGame(false)]
        [SettingIgnore] // TODO: Show as advanced setting.
        public bool NonThreadedGL { get; set; } = false;

        public string InputGui { get; set; } = "";

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

        public void CreateLaunchInDebugMode(TextMenu menu, bool inGame) {
            if (inGame || typeof(Settings).GetField("LaunchInDebugMode") == null)
                return;

            menu.Add(
                new TextMenu.OnOff(Dialog.Clean("modoptions_coremodule_launchindebugmode"), LaunchInDebugMode)
                .Change(v => LaunchInDebugMode = v)
                .NeedsRelaunch()
            );
        }

        public void CreateLaunchWithFMODLiveUpdate(TextMenu menu, bool inGame) {
            if (inGame || typeof(Settings).GetField("LaunchWithFMODLiveUpdate") == null)
                return;

            menu.Add(
                new TextMenu.OnOff(Dialog.Clean("modoptions_coremodule_launchwithfmodliveupdate"), LaunchWithFMODLiveUpdate)
                .Change(v => LaunchWithFMODLiveUpdate = v)
                .NeedsRelaunch()
            );
        }

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

    }
}
