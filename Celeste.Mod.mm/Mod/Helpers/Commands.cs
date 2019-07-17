using Monocle;

namespace Celeste.Mod.Helpers {
    internal static class Commands {

        [Command("q", "hides the command line")]
        public static void Hide() {
            Engine.Commands.Open = false;
        }

    }
}
