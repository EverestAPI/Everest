#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;

namespace Celeste {
    static class patch_PlaybackData {

        // expose the Tutorials field and vanilla methods to our patch.
#pragma warning disable CS0649 // This field is never assigned (it is in vanilla code)
        public static Dictionary<string, List<Player.ChaserState>> Tutorials;
#pragma warning restore CS0649

        public static extern void orig_Load();

        [MonoModIgnore]
        public static extern List<Player.ChaserState> Import(byte[] buffer);

        public static void Load() {
            // Vanilla Celeste uses .Add, which throws on conflicts.
            Tutorials?.Clear();

            // load vanilla tutorials
            orig_Load();

            // load mod tutorials
            if (Everest.Content.TryGet<AssetTypeDirectory>("Tutorials", out ModAsset dir, true)) {
                // crawl in the Tutorials directory
                loadTutorialsInDirectory(dir);
            }
        }

        private static void loadTutorialsInDirectory(ModAsset dir) {
            foreach (ModAsset child in dir.Children) {
                if (child.Type == typeof(AssetTypeDirectory)) {
                    // crawl in subdirectory.
                    loadTutorialsInDirectory(child);
                } else if (child.Type == typeof(AssetTypeTutorial)) {
                    // remove Tutorials/ from the tutorial path.
                    string tutorialPath = child.PathVirtual;
                    if (tutorialPath.StartsWith("Tutorials/"))
                        tutorialPath = tutorialPath.Substring("Tutorials/".Length);

                    // load tutorial.
                    Logger.Log("PlaybackData", $"Loading tutorial: {tutorialPath}");
                    byte[] buffer = child.Data;
                    List<Player.ChaserState> tutorial = Import(buffer);
                    Tutorials[tutorialPath] = tutorial;
                }
            }
        }

    }
}
