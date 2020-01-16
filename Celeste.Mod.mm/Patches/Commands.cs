#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;
using System.IO;
using FMOD.Studio;
using Monocle;
using Celeste.Mod.Meta;

namespace Celeste {
    static class patch_Commands {

        [MonoModReplace]
        [Command("capture", "capture the last ~200 frames of player movement to a file")]
        private static void CmdCapture(string filename) {
            Player player = Engine.Scene.Tracker.GetEntity<Player>();
            if (player != null) {
                PlaybackData.Export(player.ChaserStates, Path.Combine(Everest.Content.PathContentOrig, "Tutorials", filename + ".bin"));
            }
        }

        [MonoModReplace]
        [ProxyFileCalls]
        [Command("playback", "play back the file name")]
        private static void CmdPlayback(string filename) {
            filename = Path.Combine(Everest.Content.PathContentOrig, "Tutorials", filename + ".bin");
            if (File.Exists(filename)) {
                Engine.Scene = new PreviewRecording(filename);
            } else {
                Engine.Commands.Log("FILE NOT FOUND");
            }
        }
        
        [Command("print_counts", "Prints the max count for cassettes, berries, etc. To be used with a save loaded")]
        private static void CmdPrintCounts() {
            if (SaveData.Instance == null) {
                Engine.Commands.Log("No save loaded!");
                return;
            }

            LevelSetStats stats = SaveData.Instance.GetLevelSetStats();
            Engine.Commands.Log($"** Level Set Stats: {stats.Name} **");
            Engine.Commands.Log($"Max strawberry count = {stats.MaxStrawberries}");
            Engine.Commands.Log($"Max golden strawberry count = {stats.MaxGoldenStrawberries}");
            Engine.Commands.Log($"Max strawberry count including untracked = {stats.MaxStrawberriesIncludingUntracked}");
            Engine.Commands.Log($"Max cassettes = {stats.MaxCassettes}");
            Engine.Commands.Log($"Max crystal hearts = {stats.MaxHeartGems}");
            Engine.Commands.Log($"Max crystal hearts excluding C-sides = {stats.MaxHeartGemsExcludingCSides}");
            Engine.Commands.Log($"Chapter count = {stats.MaxCompletions}");
            Engine.Commands.Log("====");
            Engine.Commands.Log($"Owned strawberries = {stats.TotalStrawberries}");
            Engine.Commands.Log($"Owned golden strawberries = {stats.TotalGoldenStrawberries}");
            Engine.Commands.Log($"Owned cassettes = {stats.TotalCassettes}");
            Engine.Commands.Log($"Owned crystal hearts = {stats.TotalHeartGems}");
            Engine.Commands.Log($"Completed chapters = {stats.TotalCompletions}");
            Engine.Commands.Log("====");
            Engine.Commands.Log($"Completion percent = {stats.CompletionPercent}");
        }
    }
}
