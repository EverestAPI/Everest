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

        private static BinaryPacker.Element _Process(BinaryPacker.Element root, MapData self)
            => ((patch_MapData) self).Process(root);

        private BinaryPacker.Element Process(BinaryPacker.Element root) {
            if (Area.GetSID() == "Celeste")
                return root;

            foreach (BinaryPacker.Element el in root.Children) {
                switch (el.Name) {
                    case "levels":
                        ProcessLevels(el);
                        break;

                    case "meta":
                        ProcessMeta(el);
                        break;
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
                string levelName = level.Attr("name").Replace("lvl_", "");

                foreach (BinaryPacker.Element levelChild in level.Children) {
                    switch (levelChild.Name) {
                        case "entities":
                            foreach (BinaryPacker.Element entity in levelChild.Children) {
                                switch (entity.Name) {
                                    case "checkpoint":
                                        if (checkpointsAuto != null) {
                                            checkpointsAuto.Add(new CheckpointData(
                                                levelName,
                                                (area.GetSID() + "_" + levelName).DialogKeyify(),
                                                null,
                                                area.Dreaming,
                                                null
                                            ));
                                        }
                                        checkpoint++;
                                        strawberryInCheckpoint = 0;
                                        break;

                                    case "cassette":
                                        if (area.CassetteCheckpointIndex == 0)
                                            area.CassetteCheckpointIndex = checkpoint;
                                        break;

                                    case "strawberry":
                                        if (!entity.HasAttr("checkpointID"))
                                            entity.Attributes["checkpointID"] = checkpoint;
                                        if (!entity.HasAttr("order"))
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
                mode.Checkpoints = checkpointsAuto.ToArray();
        }

        private void ProcessMeta(BinaryPacker.Element meta) {
            AreaData area = AreaData.Get(Area);
            ModeProperties mode = area.Mode[(int) Area.Mode];

            new MapMeta(meta).ApplyTo(area);
            Area = area.ToKey();

            string sideAttr = meta.Attr("Side", "a").ToLowerInvariant();
            if (sideAttr != "a") {
                // TODO: Assign B and C side MapDatas to existing area's modes.
            }
        }

    }
}
