#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_MapData : MapData {

        public MapMetaModeProperties Meta {
            get {
                MapMeta metaAll = AreaData.Get(Area).GetMeta();
                return
                    (metaAll?.Modes?.Length ?? 0) > (int) Area.Mode ?
                    metaAll.Modes[(int) Area.Mode] :
                    null;
            }
        }

        public patch_MapData(AreaKey area)
            : base(area) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private extern void orig_Load();
        [PatchMapDataLoader] // Manually manipulate the method via MonoModRules
        private void Load() {
            try {
                orig_Load();
            } catch (Exception e) {
                Mod.Logger.Log(LogLevel.Warn, "misc", $"Failed loading MapData {Area}");
                e.LogDetailed();
            }
        }

        public extern LevelData orig_StartLevel();
        public new LevelData StartLevel() {
            MapMetaModeProperties meta = Meta;
            if (meta != null) {
                if (!string.IsNullOrEmpty(meta.StartLevel)) {
                    LevelData level = Levels.FirstOrDefault(_ => _.Name == meta.StartLevel);
                    if (level != null)
                        return level;
                }

            }

            return orig_StartLevel() ?? Levels[0];
        }

        private static BinaryPacker.Element _Process(BinaryPacker.Element root, MapData self) {
            if (self.Area.GetLevelSet() == "Celeste")
                return root;
            return ((patch_MapData) self).Process(root);
        }

        private BinaryPacker.Element Process(BinaryPacker.Element root) {
            foreach (BinaryPacker.Element el in root.Children) {
                switch (el.Name) {
                    case "levels":
                        ProcessLevels(el);
                        continue;

                    case "meta":
                        ProcessMeta(el);
                        continue;
                }
            }

            return root;
        }

        private void ProcessLevels(BinaryPacker.Element levels) {
            AreaData area = AreaData.Get(Area);
            ModeProperties mode = area.Mode[(int) Area.Mode];

            // Mod levels are... different.

            // levels.Children.Sort((a, b) => a.Attr("name").Replace("lvl_", "").CompareTo(b.Attr("name").Replace("lvl_", "")));

            int checkpoint = 0;
            List<CheckpointData> checkpointsAuto = null;
            if (mode.Checkpoints == null)
                checkpointsAuto = new List<CheckpointData>();

            int strawberry = 0;
            int strawberryInCheckpoint = 0;

            foreach (BinaryPacker.Element level in levels.Children) {
                string[] levelTags = level.Attr("name").Split(':');
                level.Attributes["name"] = levelTags[0];

                string levelName = level.Attr("name").Replace("lvl_", "");

                BinaryPacker.Element entities = level.Children.First(el => el.Name == "entities");
                BinaryPacker.Element triggers = level.Children.First(el => el.Name == "triggers");

                if (levelTags.Contains("checkpoint") || levelTags.Contains("cp"))
                    entities.Children.Add(new BinaryPacker.Element {
                        Name = "checkpoint",
                        Attributes = {
                            { "x", "0" },
                            { "y", "0" }
                        }
                    });

                if (level.AttrBool("space")) {
                    if (level.AttrBool("spaceSkipWrap") || levelTags.Contains("nospacewrap") || levelTags.Contains("nsw"))
                        entities.Children.Add(new BinaryPacker.Element {
                            Name = "everest/spaceControllerBlocker"
                        });
                    if (level.AttrBool("spaceSkipGravity") || levelTags.Contains("nospacegravity") || levelTags.Contains("nsg")) {
                        entities.Children.Add(new BinaryPacker.Element {
                            Name = "everest/spaceController"
                        });
                        level.Attributes["space"] = false;
                    }

                    if (!levelTags.Contains("nospacefix") && !levelTags.Contains("nsf") &&
                        !triggers.Children.Any(el => el.Name == "cameraTargetTrigger") &&
                        !entities.Children.Any(el => el.Name == "everest/spaceControllerBlocker")) {

                        // Camera centers tile-perfectly on uneven heights.
                        int heightForCenter = (int) level.Attributes["height"];
                        heightForCenter /= 8;
                        if (heightForCenter % 2 == 0)
                            heightForCenter--;
                        heightForCenter *= 8;

                        triggers.Children.Add(new BinaryPacker.Element {
                            Name = "cameraTargetTrigger",
                            Attributes = {
                                { "x", 0f },
                                { "y", 0f },
                                { "width", level.Attributes["width"] },
                                { "height", level.Attributes["height"] },
                                { "yOnly", true },
                                { "lerpStrength", 1f }
                            },
                            Children = {
                            new BinaryPacker.Element {
                                Attributes = {
                                    { "x", 160f },
                                    { "y", heightForCenter / 2f }
                                }
                            }
                        }
                        });
                    }
                }

                foreach (BinaryPacker.Element levelChild in level.Children) {
                    switch (levelChild.Name) {
                        case "entities":
                            foreach (BinaryPacker.Element entity in levelChild.Children) {
                                switch (entity.Name) {
                                    case "checkpoint":
                                        if (checkpointsAuto != null) {
                                            CheckpointData c = new CheckpointData(
                                                levelName,
                                                (area.GetSID() + "_" + levelName).DialogKeyify(),
                                                MapMeta.GetInventory(entity.Attr("inventory")),
                                                entity.Attr("dreaming") == "" ? area.Dreaming : entity.AttrBool("dreaming"),
                                                null
                                            );
                                            int id = entity.AttrInt("checkpointID", -1);
                                            if (id == -1) {
                                                checkpointsAuto.Add(c);
                                            } else {
                                                while (checkpointsAuto.Count <= id)
                                                    checkpointsAuto.Add(null);
                                                checkpointsAuto[id] = c;
                                            }
                                        }
                                        checkpoint++;
                                        strawberryInCheckpoint = 0;
                                        break;

                                    case "cassette":
                                        if (area.CassetteCheckpointIndex == 0)
                                            area.CassetteCheckpointIndex = checkpoint;
                                        break;

                                    case "strawberry":
                                        if (entity.AttrInt("checkpointID", -1) == -1)
                                            entity.Attributes["checkpointID"] = checkpoint;
                                        if (entity.AttrInt("order", -1) == -1)
                                            entity.Attributes["order"] = strawberryInCheckpoint;
                                        strawberry++;
                                        strawberryInCheckpoint++;
                                        break;
                                }
                            }
                            break;
                    }
                }

            }

            if (mode.Checkpoints == null)
                mode.Checkpoints = checkpointsAuto.Where(c => c != null).ToArray();
        }

        private void ProcessMeta(BinaryPacker.Element meta) {
            AreaData area = AreaData.Get(Area);
            AreaMode mode = Area.Mode;

            if (mode == AreaMode.Normal) {
                new MapMeta(meta).ApplyTo(area);
                Area = area.ToKey();
            }

            meta = meta.Children.FirstOrDefault(el => el.Name == "mode");
            if (meta == null)
                return;

            new MapMetaModeProperties(meta).ApplyTo(area, mode);
        }

    }
    public static class MapDataExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the mod mode metadata of the map.
        /// </summary>
        public static MapMetaModeProperties GetMeta(this MapData self)
            => ((patch_MapData) self).Meta;

    }
}
