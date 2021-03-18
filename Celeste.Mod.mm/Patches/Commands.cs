using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
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
            ((patch_SaveData)SaveData.Instance).LastArea_Safe = new AreaKey(id, areaMode);
            Session session = new Session(new AreaKey(id, areaMode));
            if (level != null && session.MapData.Get(level) != null) {
                bool firstLevel = level == session.MapData.StartLevel().Name;
                session = new Session(new AreaKey(id, areaMode), level) {
                    FirstLevel = firstLevel,
                    StartedFromBeginning = firstLevel
                };
            }
            Engine.Scene = new LevelLoader(session);
        }

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
                Engine.Commands.Log($"File {objPath} does not exist!");
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
    }
}
