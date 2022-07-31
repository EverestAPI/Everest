using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.Helpers {
    internal static class Commands {

        [Command("q", "hides the command line")]
        public static void Hide() {
            MInput.Keyboard.CurrentState = new KeyboardState(Keys.Enter);
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
            Everest.LogDetours();
        }
    }
}
