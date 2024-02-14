using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
             
            LevelSetStats stats = ((patch_SaveData) SaveData.Instance).LevelSetStats;
            Engine.Commands.Log(string.Format("** Level Set Stats: {0} **", stats.Name));
            Engine.Commands.Log(string.Format("Max strawberry count = {0}", stats.MaxStrawberries));
            Engine.Commands.Log(string.Format("Max golden strawberry count = {0}", stats.MaxGoldenStrawberries));
            Engine.Commands.Log(string.Format("Max strawberry count including untracked = {0}", stats.MaxStrawberriesIncludingUntracked));
            Engine.Commands.Log(string.Format("Max cassettes = {0}", stats.MaxCassettes));
            Engine.Commands.Log(string.Format("Max crystal hearts = {0}", stats.MaxHeartGems));
            Engine.Commands.Log(string.Format("Max crystal hearts excluding C-sides = {0}", stats.MaxHeartGemsExcludingCSides));
            Engine.Commands.Log(string.Format("Chapter count = {0}", stats.MaxCompletions));
            Engine.Commands.Log("====");
            Engine.Commands.Log(string.Format("Owned strawberries = {0}", stats.TotalStrawberries));
            Engine.Commands.Log(string.Format("Owned golden strawberries = {0}", stats.TotalGoldenStrawberries));
            Engine.Commands.Log(string.Format("Owned cassettes = {0}", stats.TotalCassettes));
            Engine.Commands.Log(string.Format("Owned crystal hearts = {0}", stats.TotalHeartGems));
            Engine.Commands.Log(string.Format("Completed chapters = {0}", stats.TotalCompletions));
            Engine.Commands.Log("====");
            Engine.Commands.Log(string.Format("Completion percent = {0}", stats.CompletionPercent));
        }

        // vanilla load commands: remove the [Command] attributes from them. We want to annotate ours instead.
        [MonoModReplace]
        private static void CmdLoad(int id = 0, string level = null) {
            LoadIdLevel(AreaMode.Normal, id, level);
        }

        [MonoModReplace]
        private static void CmdHard(int id = 0, string level = null) {
            LoadIdLevel(AreaMode.BSide, id, level);
        }

        [MonoModReplace]
        private static void CmdRMX2(int id = 0, string level = null) {
            LoadIdLevel(AreaMode.CSide, id, level);
        }

        // Better support for loading checkpoint room and fix vanilla game crashes when the bside/cside level does not exist
        private static void LoadIdLevel(AreaMode areaMode, int id = 0, string level = null) {
            SaveData.InitializeDebugMode();
            AreaKey areaKey = new AreaKey(id, areaMode);
            ((patch_SaveData)SaveData.Instance).LastArea_Safe = areaKey;
            Session session = new Session(areaKey);
            if (level != null && session.MapData.Get(level) != null) {
                if (AreaData.GetCheckpoint(areaKey, level) != null) {
                    session = new Session(areaKey, level) {StartCheckpoint = null};
                } else {
                    session.Level = level;
                }
                bool firstLevel = level == session.MapData.StartLevel().Name;
                session.FirstLevel = firstLevel;
                session.StartedFromBeginning = firstLevel;
            }
            Engine.Scene = new LevelLoader(session);
        }

        [Command("load", "test a level")]
        private static void CmdLoadIDorSID(string idOrSID = "0", string level = null) {
            if (int.TryParse(idOrSID, out int id)) {
                CmdLoad(id, level);
            } else {
                loadMapBySID(idOrSID, level, AreaMode.Normal, CmdLoad);
            }
        }

        [Command("hard", "test a hard level")]
        private static void CmdHardIDorSID(string idOrSID = null, string level = null) {
            if (int.TryParse(idOrSID, out int id)) {
                CmdHard(id, level);
            } else {
                loadMapBySID(idOrSID, level, AreaMode.BSide, CmdHard);
            }
        }

        [Command("rmx2", "test a RMX2 level")]
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
                Engine.Commands.Log(string.Format("Map {0} does not exist!", sid));
            } else if (mapData == null) {
                Engine.Commands.Log(string.Format("Map {0} has no {1} mode!", sid, mode));
            } else if (room != null && (mapData.Levels?.All(level => level.Name != room) ?? false)) {
                Engine.Commands.Log(string.Format("Map {0} / mode {1} has no room named {2}!", sid, mode, room));
            } else {
                // go on with the vanilla load/hard/rmx2 function.
                vanillaLoadFunction(areaData.ID, room);
            }
        }

        private class MeshData {
            public string Name;
            public List<Vector3> Vertices = new List<Vector3>();
            public List<Vector2> TextureCoordinates = new List<Vector2>();
            public List<Point> FaceData = new List<Point>();

            public void WriteTo(BinaryWriter writer) {
                writer.Write(Name);
                writer.Write(Vertices.Count);
                foreach (Vector3 vertex in Vertices) {
                    writer.Write(vertex.X);
                    writer.Write(vertex.Y);
                    writer.Write(vertex.Z);
                }
                writer.Write(TextureCoordinates.Count);
                foreach (Vector2 textureCoordinate in TextureCoordinates) {
                    writer.Write(textureCoordinate.X);
                    writer.Write(textureCoordinate.Y);
                }
                writer.Write(FaceData.Count);
                foreach (Point faceData in FaceData) {
                    writer.Write(faceData.X);
                    writer.Write(faceData.Y);
                }
            }
        }

        [Command("export_obj", "Converts an .obj model file to .obj.export")]
        private static void CmdExportObj(string objPath, string objExportPath = null) {
            if (!File.Exists(objPath)) {
                Engine.Commands.Log(string.Format("File {0} does not exist!", objPath));
            } else {
                if (objExportPath == null) {
                    objExportPath = objPath + ".export";
                }

                using (BinaryWriter exportWriter = new BinaryWriter(File.OpenWrite(objExportPath)))
                using (StreamReader objReader = new StreamReader(File.OpenRead(objPath))) {

                    List<MeshData> meshData = new List<MeshData>();
                    MeshData currentMeshData = null;

                    // read through the obj file first to collect info about all meshes.
                    string currentLine;
                    while ((currentLine = objReader.ReadLine()) != null) {
                        string[] splittedLine = currentLine.Split(' ');
                        if (splittedLine.Length == 0) {
                            continue;
                        }
                        switch (splittedLine[0]) {
                            case "o":
                                // new mesh
                                currentMeshData = new MeshData();
                                currentMeshData.Name = splittedLine[1];
                                meshData.Add(currentMeshData);
                                break;
                            case "v":
                                // new vertex
                                Vector3 vertex = new Vector3(Float(splittedLine[1]), Float(splittedLine[2]), Float(splittedLine[3]));
                                currentMeshData.Vertices.Add(vertex);
                                break;
                            case "vt":
                                // new texture coordinate
                                Vector2 textureCoordinate = new Vector2(Float(splittedLine[1]), Float(splittedLine[2]));
                                currentMeshData.TextureCoordinates.Add(textureCoordinate);
                                break;
                            case "f":
                                // new polygonal face
                                for (int i = 1; i < Math.Min(4, splittedLine.Length); i++) {
                                    Point currentFaceData = new Point();
                                    string[] faceDataSplit = splittedLine[i].Split('/');
                                    if (faceDataSplit[0].Length > 0) {
                                        currentFaceData.X = int.Parse(faceDataSplit[0]);
                                    }
                                    if (faceDataSplit[1].Length > 0) {
                                        currentFaceData.Y = int.Parse(faceDataSplit[1]);
                                    }
                                    currentMeshData.FaceData.Add(currentFaceData);
                                }
                                break;
                        }
                    }

                    // we now read through the obj file! time to export it.
                    exportWriter.Write(meshData.Count);
                    foreach (MeshData data in meshData) {
                        data.WriteTo(exportWriter);
                    }
                }
            }
        }

        private static float Float(string data) {
            return float.Parse(data, CultureInfo.InvariantCulture);
        }

        [Command("mainmenu", "go to the main menu")]
        private static void CmdMainMenu() {
            Engine.Scene = new OverworldLoader(Overworld.StartMode.MainMenu);
        }

        [Command("returntomap", "return to map")]
        private static void ReturnToMap() {
            if (SaveData.Instance == null) {
                SaveData.InitializeDebugMode();
                SaveData.Instance.CurrentSession = new Session(AreaKey.Default);
            }

            Engine.Scene = new OverworldLoader(Overworld.StartMode.AreaQuit);
        }

        [MonoModIgnore]
        [RemoveCommandAttribute]
        private static extern void CmdHearts(int amount);

        [MonoModReplace]
        [Command("hearts", "sets the amount of obtained hearts for the specified level set to a given number (default all hearts and current level set)")]
        private static void CmdHearts(int amount = int.MaxValue, string levelSet = null) {
            patch_SaveData saveData = patch_SaveData.Instance;
            if (saveData == null)
                return;

            if (string.IsNullOrEmpty(levelSet))
                levelSet = saveData.LevelSet;

            int num = 0;
            foreach (patch_AreaStats areaStats in saveData.Areas_Safe.Cast<patch_AreaStats>().Where(stats => stats.LevelSet == levelSet)) {
                for (int i = 0; i < areaStats.Modes.Length; i++) {
                    if (AreaData.Get(areaStats.ID).Mode is not {} mode || mode.Length <= i || mode[i]?.MapData == null)
                        continue;

                    AreaModeStats areaModeStats = areaStats.Modes[i];
                    if (num < amount) {
                        areaModeStats.HeartGem = true;
                        num++;
                    } else {
                        areaModeStats.HeartGem = false;
                    }
                }
            }
        }

        [Command("openlog", "open log.txt file")]
        private static void CmdOpenLog() {
            string pathLog = Everest.PathLog;
            if (File.Exists(pathLog)) {
                ProcessStartInfo startInfo = new() {
                    FileName = pathLog,
                    UseShellExecute = true,
                };

                Process.Start(startInfo);
            }
            else
                Engine.Commands.Log($"{pathLog} does not exist");
        }
    }
}