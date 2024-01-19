#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;

namespace Celeste {
    static class patch_PlaybackData {

        // expose the Tutorials field and vanilla methods to our patch.
        public static Dictionary<string, List<Player.ChaserState>> Tutorials;

        private static T _<T>(string info, Func<T> func) {
            bool err = true;
            try {
                T value = func();

                if (value is string s && s.Length > 1024) {
                    throw new InvalidDataException("String longer than 1024 characters!");
                }

                Logger.Verbose("PlaybackData.ImportVerbose", $"{info} = {value}");
                err = false;
                return value;
            } finally {
                if (err)
                    Logger.Verbose("PlaybackData.ImportVerbose", $"{info} ERROR");
            }
        }

        public static List<Player.ChaserState> ImportVerbose(byte[] buffer) {
            using (BinaryReader reader = new BinaryReader(new MemoryStream(buffer))) {
                List<Player.ChaserState> list = new List<Player.ChaserState>();

                int version = 1;
                if (_("HEADER", reader.ReadString) == "TIMELINE") {
                    version = reader.ReadInt32();
                } else {
                    reader.BaseStream.Seek(0L, SeekOrigin.Begin);
                }

                int frameCount = reader.ReadInt32();
                for (int i = 0; i < frameCount; i++) {
                    Player.ChaserState state = default;
                    state.Position.X = _("Position.X", reader.ReadSingle);
                    state.Position.Y = _("Position.Y", reader.ReadSingle);
                    state.TimeStamp = _("TimeStamp", reader.ReadSingle);
                    state.Animation = _("Animation", reader.ReadString);
                    state.Facing = (Facings) _("Facing", reader.ReadInt32);
                    state.OnGround = _("OnGround", reader.ReadBoolean);
                    state.HairColor = new Color(_("HairColor.R", reader.ReadByte), _("HairColor.G", reader.ReadByte), _("HairColor.B", reader.ReadByte), 255);
                    state.Depth = _("Depth", reader.ReadInt32);
                    state.Sounds = 0;
                    if (version == 1) {
                        state.Scale = new Vector2((float) state.Facing, 1f);
                        state.DashDirection = Vector2.Zero;
                    } else {
                        state.Scale.X = _("Scale.X", reader.ReadSingle);
                        state.Scale.Y = _("Scale.Y", reader.ReadSingle);
                        state.DashDirection.X = _("DashDirection.X", reader.ReadSingle);
                        state.DashDirection.Y = _("DashDirection.Y", reader.ReadSingle);
                    }
                    list.Add(state);
                }

                return list;
            }
        }

        public static extern List<Player.ChaserState> orig_Import(byte[] buffer);
        public static List<Player.ChaserState> Import(byte[] buffer) {
            try {
                return orig_Import(buffer);
            } catch {
                try {
                    return ImportVerbose(buffer);
                } catch (Exception e) {
                    Logger.LogDetailed(e);
                    return null;
                }
            }
        }

        [MonoModReplace]
        public static void Load() {
            // Vanilla Celeste uses .Add, which throws on conflicts.
            Tutorials?.Clear();

            // load vanilla tutorials
            foreach (string path in Directory.GetFiles(Path.Combine(Engine.ContentDirectory, "Tutorials"))) {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                Logger.Verbose("PlaybackData", $"Loading vanilla tutorial: {fileNameWithoutExtension}");

                List<Player.ChaserState> tutorial = Import(File.ReadAllBytes(path));
                if (tutorial != null)
                    PlaybackData.Tutorials.Add(fileNameWithoutExtension, tutorial);
            }

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
                    Logger.Verbose("PlaybackData", $"Loading tutorial: {tutorialPath}");
                    List<Player.ChaserState> tutorial = Import(child.Data);
                    if (tutorial != null)
                        Tutorials[tutorialPath] = tutorial;
                }
            }
        }

    }
}
