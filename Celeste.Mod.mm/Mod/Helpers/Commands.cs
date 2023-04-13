using Microsoft.Xna.Framework.Input;
using Monocle;
using System;

namespace Celeste.Mod.Helpers {
    internal static class Commands {

        [Command("q", "hides the command line")]
        public static void Hide() {
            MInput.Keyboard.CurrentState = new KeyboardState(Keys.Enter); // trick MInput into thinking Enter was already pressed on previous frame to prevent registering new press
            Engine.Commands.Open = false;
        }

        [Command("wipedebug", "wipes the debug save")]
        public static void WipeDebug() {
            SaveData.TryDelete(-1);
            if (SaveData.Instance != null && SaveData.Instance.FileSlot == -1)
                SaveData.InitializeDebugMode(true);
        }

        [Command("logdetours", "log all detours / hooks to output")]
        public static void LogDetours() {
            Everest.LogDetours(Logger.GetLogLevel("detours"));
        }

        [Command("setloglevel", "sets the minimum log level for a tag prefix (use * to match all)")]
        public static void SetLogLevel(string tagPrefix, string minimumLevel = "Verbose") {
            if (Enum.TryParse(minimumLevel, ignoreCase: true, out LogLevel result)) {
                tagPrefix = tagPrefix.Equals("*") ? "" : tagPrefix;
                // We treat our command log levels as settings to make sure they get prioritized
                Logger.SetLogLevelFromSettings(tagPrefix, result);
            } else
                Engine.Commands.Log($"Invalid log level! Use Verbose, Debug, Info, Warn, or Error");
        }

        [Command("modoptions", "toggles Everest's 'Show Mod Options in Game' setting")]
        public static void ToggleShowModOptions() {
            Core.CoreModule.Settings.ShowModOptionsInGame = !Core.CoreModule.Settings.ShowModOptionsInGame;
        }
    }
}
