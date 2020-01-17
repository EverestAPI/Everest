using Celeste.Mod.Meta;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.Core {
    public class CoreMapDataProcessor : EverestMapDataProcessor {

        public int Checkpoint;
        public int Strawberry;
        public int StrawberryInCheckpoint;
        public List<CheckpointData> CheckpointsAuto;
        public string[] LevelTags;
        public string LevelName;
        public int TotalStrawberriesIncludingUntracked;

        public override void Reset() {
            Checkpoint = 0;
            Strawberry = 0;
            StrawberryInCheckpoint = 0;
            CheckpointsAuto = Mode.Checkpoints == null ? new List<CheckpointData>() : null;
            TotalStrawberriesIncludingUntracked = 0;
        }

        public override Dictionary<string, Action<BinaryPacker.Element>> Init()
            => new Dictionary<string, Action<BinaryPacker.Element>>() {
                { "root", root => {
                    foreach (BinaryPacker.Element el in root.Children)
                        Context.Run(el.Name, el);
                } },

                { "Style", style => {
                    // Celeste 1.2.5.0 optimizes BinaryPacker, which causes some issues.
                    // Let's "unoptimize" Style and its Backgrounds and Foregrounds.
                    if (style.Children == null)
                        style.Children = new List<BinaryPacker.Element>();
                    foreach (BinaryPacker.Element el in style.Children)
                        if ((   el.Name == "Backgrounds" ||
                                el.Name == "Foregrounds") &&
                                el.Children == null)
                            el.Children = new List<BinaryPacker.Element>();
                } },

                { "levels", levels => {
                    if (levels.Children != null) {
                        foreach (BinaryPacker.Element level in levels.Children) {
                            Context.Run("level", level);

                            if (level.Children != null)
                                foreach (BinaryPacker.Element levelChild in level.Children)
                                    Context.Run(levelChild.Name, levelChild);
                        }
                    }
                } },

                { "level", level => {
                    // lvl_ was optional before Celeste 1.2.5.0 made it mandatory.
                    // Certain level "tags" are supported as very early mods used them.
                    LevelTags = level.Attr("name").Split(':');
                    LevelName = LevelTags[0];
                    if (LevelName.StartsWith("lvl_"))
                        LevelName = LevelName.Substring(4);
                    level.SetAttr("name", "lvl_" + LevelName);

                    BinaryPacker.Element entities = level.Children.FirstOrDefault(el => el.Name == "entities");
                    BinaryPacker.Element triggers = level.Children.FirstOrDefault(el => el.Name == "triggers");

                    // Celeste 1.2.5.0 optimizes BinaryPacker (null instead of empty lists),
                    // which causes some issues where the game still expects an empty list.
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

                    if (LevelTags.Contains("checkpoint") || LevelTags.Contains("cp"))
                        entities.Children.Add(new BinaryPacker.Element {
                            Name = "checkpoint",
                            Attributes = new Dictionary<string, object>() {
                                    { "x", "0" },
                                    { "y", "0" }
                                }
                        });

                    if (level.AttrBool("space")) {
                        if (level.AttrBool("spaceSkipWrap") || LevelTags.Contains("nospacewrap") || LevelTags.Contains("nsw"))
                            entities.Children.Add(new BinaryPacker.Element {
                                Name = "everest/spaceControllerBlocker"
                            });
                        if (level.AttrBool("spaceSkipGravity") || LevelTags.Contains("nospacegravity") || LevelTags.Contains("nsg")) {
                            entities.Children.Add(new BinaryPacker.Element {
                                Name = "everest/spaceController"
                            });
                            level.SetAttr("space", false);
                        }

                        if (!LevelTags.Contains("nospacefix") && !LevelTags.Contains("nsf") &&
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
                } },

                { "entities", levelChild => {
                    // check if the room has a checkpoint first.
                    foreach (BinaryPacker.Element entity in levelChild.Children) {
                        if (entity.Name == "checkpoint") {
                            if (CheckpointsAuto != null) {
                                MapMeta modeMeta = AreaData.GetModeMeta(AreaKey.Mode);
                                CheckpointData c = new CheckpointData(
                                    LevelName,
                                    (AreaData.GetSID() + "_" + LevelName).DialogKeyify(),
                                    MapMeta.GetInventory(entity.Attr("inventory")),
                                    entity.Attr("dreaming") == "" ? modeMeta.Dreaming ?? AreaData.Dreaming : entity.AttrBool("dreaming"),
                                    null
                                );
                                if (entity.Attr("coreMode") == "") {
                                    c.CoreMode = modeMeta.CoreMode ?? AreaData.CoreMode;
                                } else {
                                    entity.AttrIf("coreMode", v => c.CoreMode = (Session.CoreModes) Enum.Parse(typeof(Session.CoreModes), v, true));
                                }

                                int id = entity.AttrInt("checkpointID", -1);
                                if (id == -1) {
                                    CheckpointsAuto.Add(c);
                                } else {
                                    while (CheckpointsAuto.Count <= id)
                                        CheckpointsAuto.Add(null);
                                    CheckpointsAuto[id] = c;
                                }
                            }
                            Checkpoint++;
                            StrawberryInCheckpoint = 0;
                        }
                    }

                    // then, auto-assign strawberries and cassettes to checkpoints.
                    foreach (BinaryPacker.Element entity in levelChild.Children)
                        Context.Run("entity:" + entity.Name, entity);
                } },

                { "entity:cassette", entity => {
                    if (AreaData.CassetteCheckpointIndex < 0)
                        AreaData.CassetteCheckpointIndex = Checkpoint;

                    Context.MapData.SetDetectedCassette();
                } },

                { "entity:strawberry", entity => {
                    if (!entity.AttrBool("moon", false))
                    {
                        if (entity.AttrInt("checkpointID", -1) == -1)
                            entity.SetAttr("checkpointID", Checkpoint);
                        if (entity.AttrInt("order", -1) == -1)
                            entity.SetAttr("order", StrawberryInCheckpoint);
                        Strawberry++;
                        StrawberryInCheckpoint++;
                    }
                } }
            };

        public override void Run(string stepName, BinaryPacker.Element el)
        {
            if (StrawberryRegistry.TrackableContains(el))
                stepName = "entity:strawberry";

            if (StrawberryRegistry.GetRegisteredBerries().Any(berry => berry.entityName == el.Name))
                TotalStrawberriesIncludingUntracked++;

            base.Run(stepName, el);
        }

        public override void End() {
            if (Mode.Checkpoints == null)
                Mode.Checkpoints = CheckpointsAuto.Where(c => c != null).ToArray();

            Context.MapData.SetDetectedStrawberriesIncludingUntracked(TotalStrawberriesIncludingUntracked);
        }
    }
}
