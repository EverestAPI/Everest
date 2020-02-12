#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_MapData : MapData {

        public bool DetectedCassette;
        public int DetectedStrawberriesIncludingUntracked;
        public List<EntityData> DashlessGoldenberries = new List<EntityData>();

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

        [PatchTrackableStrawberryCheck]
        private extern void orig_Load();

        [PatchMapDataLoader] // Manually manipulate the method via MonoModRules
        private void Load() {
            // reset those fields to prevent them from stacking up when reloading the map.
            DetectedStrawberries = 0;
            DetectedHeartGem = false;
            DetectedRemixNotes = false;
            Goldenberries = new List<EntityData>();
            DashlessGoldenberries = new List<EntityData>();
            DetectedCassette = false;
            DetectedStrawberriesIncludingUntracked = 0;

            try {
                orig_Load();

                foreach (LevelData level in Levels) {
                    foreach (EntityData entity in level.Entities) {
                        if (entity.Name == "memorialTextController") // aka "dashless golden"
                            DashlessGoldenberries.Add(entity);
                    }
                }

                AreaData area = AreaData.Get(Area);
                AreaData parentArea = AreaDataExt.Get(area.GetMeta()?.Parent);
                ModeProperties parentMode = parentArea?.Mode?.ElementAtOrDefault((int) Area.Mode);
                if (parentMode != null) {
                    MapData parentMapData = parentMode.MapData;

                    parentMapData.Strawberries.AddRange(Strawberries);

                    // Recount everything berry-related for the parent map data, just like in orig_Load.
                    parentMode.TotalStrawberries = 0;
                    parentMode.StartStrawberries = 0;
                    parentMode.StrawberriesByCheckpoint = new EntityData[10, 25];

                    for (int i = 0; parentMode.Checkpoints != null && i < parentMode.Checkpoints.Length; i++)
                        if (parentMode.Checkpoints[i] != null)
                            parentMode.Checkpoints[i].Strawberries = 0;

                    foreach (EntityData entity in parentMapData.Strawberries) {
                        if (!entity.Bool("moon")) {
                            int checkpointID = entity.Int("checkpointIDParented", entity.Int("checkpointID"));
                            int order = entity.Int("order");

                            if (_GrowAndGet(ref parentMode.StrawberriesByCheckpoint, checkpointID, order) == null)
                                parentMode.StrawberriesByCheckpoint[checkpointID, order] = entity;

                            if (checkpointID == 0)
                                parentMode.StartStrawberries++;
                            else if (parentMode.Checkpoints != null)
                                parentMode.Checkpoints[checkpointID - 1].Strawberries++;

                            parentMode.TotalStrawberries++;
                        }
                    }
                }

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

            // make sure parse meta first, because checkpoint entity needs to read meta
            if (root.Children.Find(element => element.Name == "meta") is BinaryPacker.Element meta)
                ProcessMeta(meta);

            new MapDataFixup(this).Process(root);

            return root;
        }

        private void ProcessMeta(BinaryPacker.Element meta) {
            AreaData area = AreaData.Get(Area);
            AreaMode mode = Area.Mode;

            if (mode == AreaMode.Normal) {
                new MapMeta(meta).ApplyTo(area);
                Area = area.ToKey();

                // Backup A-Side's Metadata. Only back up useful data.
                area.SetASideAreaDataBackup(new AreaData {
                    IntroType = area.IntroType,
                    ColorGrade = area.ColorGrade,
                    DarknessAlpha = area.DarknessAlpha,
                    BloomBase = area.BloomBase,
                    BloomStrength = area.BloomStrength,
                    CoreMode = area.CoreMode,
                    Dreaming = area.Dreaming
                });
            }

            BinaryPacker.Element modeMeta = meta.Children?.FirstOrDefault(el => el.Name == "mode");
            if (modeMeta == null)
                return;

            new MapMetaModeProperties(modeMeta).ApplyTo(area, mode);

            // Metadata for B-Side and C-Side are parsed and stored.
            if (mode != AreaMode.Normal) {
                MapMeta mapMeta = new MapMeta(meta) {
                    Modes = area.GetMeta().Modes
                };
                area.Mode[(int) mode].SetMapMeta(mapMeta);
            }
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchBackdropParser] // ... except for manually manipulating the method via MonoModRules
        private extern Backdrop ParseBackdrop(BinaryPacker.Element child, BinaryPacker.Element above);

        private static EntityData _GrowAndGet(ref EntityData[,] map, int y, int x) {
            if (y < 0)
                y = -y;
            if (x < 0)
                x = -x;

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

        public static Backdrop LoadCustomBackdrop(BinaryPacker.Element child, BinaryPacker.Element above, MapData map) {
            Backdrop backdropFromMod = Everest.Events.Level.LoadBackdrop(map, child, above);
            if (backdropFromMod != null)
                return backdropFromMod;

            if (child.Name.Equals("rain", StringComparison.OrdinalIgnoreCase)) {
                patch_RainFG rain = new patch_RainFG();
                if (child.HasAttr("color"))
                    rain.Color = Calc.HexToColor(child.Attr("color"));
                return rain;
            }

            return null;
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

        /// <summary>
        /// Returns whether the map contains a cassette or not.
        /// </summary>
        public static bool GetDetectedCassette(this MapData self)
            => ((patch_MapData) self).DetectedCassette;

        /// <summary>
        /// To be called by the CoreMapDataProcessor when a cassette is detected in a map.
        /// </summary>
        internal static void SetDetectedCassette(this MapData self) {
            ((patch_MapData) self).DetectedCassette = true;
        }

        /// <summary>
        /// Returns the number of strawberries in the map, including untracked ones (goldens, moons).
        /// </summary>
        public static int GetDetectedStrawberriesIncludingUntracked(this MapData self)
            => ((patch_MapData) self).DetectedStrawberriesIncludingUntracked;

        /// <summary>
        /// To be called by the CoreMapDataProcessor when processing a map is over, to register the detected berry count.
        /// </summary>
        internal static void SetDetectedStrawberriesIncludingUntracked(this MapData self, int count) {
            ((patch_MapData) self).DetectedStrawberriesIncludingUntracked = count;
        }

        /// <summary>
        /// Returns the list of dashless goldens in the map.
        /// </summary>
        public static List<EntityData> GetDashlessGoldenberries(this MapData self)
            => ((patch_MapData) self).DashlessGoldenberries;
    }
}
