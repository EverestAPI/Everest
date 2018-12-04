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
            if (root.Children == null)
                return root;

            foreach (BinaryPacker.Element el in root.Children) {
                switch (el.Name) {
                    case "levels":
                        ProcessLevels(el);
                        continue;

                    case "meta":
                        ProcessMeta(el);
                        continue;

                    // Celeste 1.2.5.0 optimizes BinaryPacker, which causes some issues.
                    // Let's "unoptimize" entities and triggers.
                    case "Backgrounds":
                    case "Foregrounds":
                        if (el.Children == null)
                            el.Children = new List<BinaryPacker.Element>();
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

            if (levels.Children != null) {
                foreach (BinaryPacker.Element level in levels.Children) {
                    string[] levelTags = level.Attr("name").Split(':');
                    string levelName = levelTags[0];
                    if (levelName.StartsWith("lvl_"))
                        levelName = levelName.Substring(4);
                    level.SetAttr("name", "lvl_" + levelName); // lvl_ was optional before Celeste 1.2.5.0 made it mandatory.

                    BinaryPacker.Element entities = level.Children.FirstOrDefault(el => el.Name == "entities");
                    BinaryPacker.Element triggers = level.Children.FirstOrDefault(el => el.Name == "triggers");
                    
                    // Celeste 1.2.5.0 optimizes BinaryPacker, which causes some issues.
                    // Let's "unoptimize" entities and triggers.
                    if (entities == null)
                        level.Children.Add(entities = new BinaryPacker.Element {
                            Name = "entities"
                        });
                    if (entities.Children == null)
                        entities.Children = new List<BinaryPacker.Element>();

                    if (triggers == null)
                        level.Children.Add(triggers = new BinaryPacker.Element {
                            Name = "triggers"
                        });
                    if (triggers.Children == null)
                        triggers.Children = new List<BinaryPacker.Element>();

                    if (levelTags.Contains("checkpoint") || levelTags.Contains("cp"))
                        entities.Children.Add(new BinaryPacker.Element {
                            Name = "checkpoint",
                            Attributes = new Dictionary<string, object>() {
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
                            level.SetAttr("space", false);
                        }

                        if (!levelTags.Contains("nospacefix") && !levelTags.Contains("nsf") &&
                            !triggers.Children.Any(el => el.Name == "cameraTargetTrigger") &&
                            !entities.Children.Any(el => el.Name == "everest/spaceControllerBlocker")) {

                            // Camera centers tile-perfectly on uneven heights.
                            int heightForCenter = level.AttrInt("height");
                            heightForCenter /= 8;
                            if (heightForCenter % 2 == 0)
                                heightForCenter--;
                            heightForCenter *= 8;

                            triggers.Children.Add(new BinaryPacker.Element {
                                Name = "cameraTargetTrigger",
                                Attributes = new Dictionary<string, object>() {
                                    { "x", 0f },
                                    { "y", 0f },
                                    { "width", level.AttrInt("width") },
                                    { "height", level.AttrInt("height") },
                                    { "yOnly", true },
                                    { "lerpStrength", 1f }
                                },
                                Children = new List<BinaryPacker.Element>() {
                                    new BinaryPacker.Element {
                                        Attributes = new Dictionary<string, object>() {
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
                                                entity.SetAttr("checkpointID", checkpoint);
                                            if (entity.AttrInt("order", -1) == -1)
                                                entity.SetAttr("order", strawberryInCheckpoint);
                                            strawberry++;
                                            strawberryInCheckpoint++;
                                            break;
                                    }
                                }
                                break;
                        }
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

            meta = meta.Children?.FirstOrDefault(el => el.Name == "mode");
            if (meta == null)
                return;

            new MapMetaModeProperties(meta).ApplyTo(area, mode);
        }

        private static EntityData _GrowAndGet(ref EntityData[,] map, int y, int x) {
            if (map.GetLength(0) <= y || map.GetLength(1) <= x) {
                // Array.Resize is unavailable and Copy sees the entire array as one row.
                EntityData[,] mapNew = new EntityData[y + 10, x + 25];
                int ho = map.GetLength(1);
                int hn = mapNew.GetLength(1);
                int wo = map.GetLength(0);
                for (int co = 0; co < wo; co++)
                    Array.Copy(map, co * ho, mapNew, co * hn, ho);
                map = mapNew;
            }

            return map[y, x];
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
