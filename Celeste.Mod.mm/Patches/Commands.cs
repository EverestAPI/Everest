using Celeste.Mod;
using Monocle;
using MonoMod;
using System;
using System.IO;
using System.Linq;

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

        // vanilla load commands: don't touch their bodies, but remove the [Command] attributes from them. We want to annotate ours instead.
        [MonoModIgnore]
        private static extern void CmdLoad(int id = 0, string level = null);
        [MonoModIgnore]
        private static extern void CmdHard(int id = 0, string level = null);
        [MonoModIgnore]
        private static extern void CmdRMX2(int id = 0, string level = null);

        [Command("load", "test a level")]
        [RemoveCommandAttributeFromVanillaLoadMethod]
        private static void CmdLoadIDorSID(string idOrSID = "0", string level = null) {
            if (int.TryParse(idOrSID, out int id)) {
                CmdLoad(id, level);
            } else {
                loadMapBySID(idOrSID, level, AreaMode.Normal, CmdLoad);
            }
        }

        [Command("hard", "test a hard level")]
        [RemoveCommandAttributeFromVanillaLoadMethod]
        private static void CmdHardIDorSID(string idOrSID = null, string level = null) {
            if (int.TryParse(idOrSID, out int id)) {
                CmdHard(id, level);
            } else {
                loadMapBySID(idOrSID, level, AreaMode.BSide, CmdHard);
            }
        }

        [Command("rmx2", "test a RMX2 level")]
        [RemoveCommandAttributeFromVanillaLoadMethod]
        private static void CmdRMX2IDorSID(string idOrSID = null, string level = null) {
            if (int.TryParse(idOrSID, out int id)) {
                CmdRMX2(id, level);
            } else {
                loadMapBySID(idOrSID, level, AreaMode.CSide, CmdRMX2);
            }
        }

        private static void loadMapBySID(string sid, string room, AreaMode mode, Action<int, string> vanillaLoadFunction) {
            if (sid == null) {
                Engine.Commands.Log("Please specify a map ID or SID.");
                return;
            }

            AreaData areaData = patch_AreaData.Get(sid);
            MapData mapData = null;
            if (areaData?.Mode.Length > (int) mode) {
                mapData = areaData?.Mode[(int) mode]?.MapData;
            }
            if (areaData == null) {
                Engine.Commands.Log($"Map {sid} does not exist!");
            } else if (mapData == null) {
                Engine.Commands.Log($"Map {sid} has no {mode} mode!");
            } else if (room != null && (mapData.Levels?.All(level => level.Name != room) ?? false)) {
                Engine.Commands.Log($"Map {sid} / mode {mode} has no room named {room}!");
            } else {
                // go on with the vanilla load/hard/rmx2 function.
                vanillaLoadFunction(areaData.ID, room);
            }
        }
    }
}
