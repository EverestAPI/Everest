#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Celeste.Mod.Helpers;

namespace Celeste {
    public class patch_MapData : MapData {

        public bool DetectedCassette;
        public int DetectedStrawberriesIncludingUntracked;
        public List<EntityData> DashlessGoldenberries = new List<EntityData>();

        public delegate Backdrop BackdropLoader(BinaryPacker.Element data);
        public static readonly Dictionary<string, BackdropLoader> BackdropLoaders = new Dictionary<string, BackdropLoader>();

        private Dictionary<string, LevelData> levelsByName = new Dictionary<string, LevelData>();

        public MapMetaModeProperties Meta {
            get {
                MapMeta metaAll = patch_AreaData.Get(Area).Meta;
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
        [PatchMapDataLoader] // Manually manipulate the method via MonoModRules
        private extern void orig_Load();

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

                RegenerateLevelsByNameCache();

                foreach (LevelData level in Levels) {
                    foreach (EntityData entity in level.Entities) {
                        if (entity.Name == "memorialTextController") // aka "dashless golden"
                            DashlessGoldenberries.Add(entity);
                    }
                }

                patch_AreaData area = patch_AreaData.Get(Area);
                AreaData parentArea = patch_AreaData.Get(area.Meta?.Parent);
                ModeProperties parentMode = parentArea?.Mode?.ElementAtOrDefault((int) Area.Mode);
                if (parentMode != null) {
                    MapData parentMapData = parentMode.MapData;
                    if (parentMapData == null) {
                        Logger.Log(LogLevel.Warn, "MapData", $"Failed auto-assigning data from {Area} to its unloaded parent");
                        return;
                    }

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

            } catch (Exception e) when (e is not OutOfMemoryException) { // OOM errors are currently unrecoverable
                Logger.Log(LogLevel.Warn, "MapData", $"Failed loading MapData {Area}");
                e.LogDetailed();
            }
        }

        public extern LevelData orig_StartLevel();
        public new LevelData StartLevel() {
            MapMetaModeProperties meta = Meta;
            LevelData level;
            if (!string.IsNullOrEmpty(meta?.StartLevel)) {
                level = Levels.FirstOrDefault(lvl => lvl.Name == meta.StartLevel);
                if (level != null)
                    return level;

                Logger.Log(LogLevel.Warn, "MapData", $"The starting room defined in metadata, \"{meta.StartLevel}\", does not exist for map {((patch_AreaData) Data)?.SID}!");
            }

            level = orig_StartLevel();
            if (level != null)
                return level;

            Logger.Log(LogLevel.Debug, "MapData", $"There is no room at (0,0) in map {((patch_AreaData) Data)?.SID}, attempting fallback to the first room.");
            level = Levels.FirstOrDefault();

            if (level == null) {
                Logger.Log(LogLevel.Warn, "MapData", $"Map {((patch_AreaData) Data)?.SID} has no rooms!");
            }
            return level;
        }

        [MonoModReplace]
        public new LevelData Get(string levelName) {
            if (levelsByName is null)
                RegenerateLevelsByNameCache();

            if (levelsByName.TryGetValue(levelName, out LevelData level))
                return level;

            return null;
        }

        public void RegenerateLevelsByNameCache() {
            if (levelsByName is null) {
                levelsByName = new Dictionary<string, LevelData>();
            } else {
                levelsByName.Clear();
            }

            foreach (LevelData level in Levels) {
                if (!levelsByName.ContainsKey(level.Name)) {
                    levelsByName.Add(level.Name, level);
                } else {
                    Logger.Log(LogLevel.Warn, "MapData", $"Failed to load duplicate room name {level.Name} in map {((patch_AreaData) Data)?.SID}");
                }
            }
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
            patch_AreaData area = patch_AreaData.Get(Area);
            AreaMode mode = Area.Mode;

            if (mode == AreaMode.Normal) {
                new MapMeta(meta).ApplyTo(area);
                Area = area.ToKey();

                // Backup A-Side's Metadata. Only back up useful data.
                area.ASideAreaDataBackup = new AreaData {
                    IntroType = area.IntroType,
                    ColorGrade = area.ColorGrade,
                    DarknessAlpha = area.DarknessAlpha,
                    BloomBase = area.BloomBase,
                    BloomStrength = area.BloomStrength,
                    CoreMode = area.CoreMode,
                    Dreaming = area.Dreaming
                };
            }

            BinaryPacker.Element modeMeta = meta.Children?.FirstOrDefault(el => el.Name == "mode");
            if (modeMeta == null)
                return;

            new MapMetaModeProperties(modeMeta).ApplyTo(area, mode);

            // Metadata for B-Side and C-Side are parsed and stored.
            if (mode != AreaMode.Normal) {
                MapMeta mapMeta = new MapMeta(meta) {
                    Modes = area.Meta.Modes
                };
                area.Mode[(int) mode].MapMeta = mapMeta;
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

            if (BackdropLoaders.TryGetValue(child.Name, out BackdropLoader loader)) {
                Backdrop loaded = loader(child);
                if (loaded != null)
                    return loaded;
            }

            if (child.Name.Equals("rain", StringComparison.OrdinalIgnoreCase)) {
                patch_RainFG rain = new patch_RainFG();
                if (child.HasAttr("color"))
                    rain.Color = Calc.HexToColor(child.Attr("color"));
                return rain;
            }

            return null;
        }

        public static void ParseTags(BinaryPacker.Element child, Backdrop backdrop) {
            foreach (string tag in child.Attr("tag").Split(',')) {
                backdrop.Tags.Add(tag);
            }
        }
    }
    public static class MapDataExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the mod mode metadata of the map.
        /// </summary>
        [Obsolete("Use MapData.Meta instead.")]
        public static MapMetaModeProperties GetMeta(this MapData self)
            => ((patch_MapData) self).Meta;

        /// <summary>
        /// Returns whether the map contains a cassette or not.
        /// </summary>
        [Obsolete("Use MapData.DetectedCassette instead.")]
        public static bool GetDetectedCassette(this MapData self)
            => ((patch_MapData) self).DetectedCassette;

        /// <summary>
        /// To be called by the CoreMapDataProcessor when a cassette is detected in a map.
        /// </summary>
        [Obsolete("Use MapData.DetectedCassette instead.")]
        internal static void SetDetectedCassette(this MapData self) {
            ((patch_MapData) self).DetectedCassette = true;
        }

        /// <summary>
        /// Returns the number of strawberries in the map, including untracked ones (goldens, moons).
        /// </summary>
        [Obsolete("Use MapData.DetectedStrawberriesIncludingUntracked instead.")]
        public static int GetDetectedStrawberriesIncludingUntracked(this MapData self)
            => ((patch_MapData) self).DetectedStrawberriesIncludingUntracked;

        /// <summary>
        /// To be called by the CoreMapDataProcessor when processing a map is over, to register the detected berry count.
        /// </summary>
        [Obsolete("Use MapData.DetectedStrawberriesIncludingUntracked instead.")]
        internal static void SetDetectedStrawberriesIncludingUntracked(this MapData self, int count) {
            ((patch_MapData) self).DetectedStrawberriesIncludingUntracked = count;
        }

        /// <summary>
        /// Returns the list of dashless goldens in the map.
        /// </summary>
        [Obsolete("Use MapData.DashlessGoldenBerries instead.")]
        public static List<EntityData> GetDashlessGoldenberries(this MapData self)
            => ((patch_MapData) self).DashlessGoldenberries;
    }
}

namespace MonoMod {
    /// <summary>
    /// Check for ldstr "Corrupted Level Data" and pop the throw after that.
    /// Also manually execute ProxyFileCalls rule.
    /// Also includes a patch for the strawberry tracker.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchMapDataLoader))]
    class PatchMapDataLoaderAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchMapDataLoader(MethodDefinition method, CustomAttribute attrib) {
            ProxyFileCalls(method, attrib);

            MethodDefinition m_Process = method.DeclaringType.FindMethod("Celeste.BinaryPacker/Element _Process(Celeste.BinaryPacker/Element,Celeste.MapData)");
            MethodDefinition m_GrowAndGet = method.DeclaringType.FindMethod("Celeste.EntityData _GrowAndGet(Celeste.EntityData[0...,0...]&,System.Int32,System.Int32)");

            bool corruptedLevelDataFound = false;
            bool binaryPackerFound = false;
            bool strawberriesByCheckpointFound = false;

            bool pop = false;
            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.MatchLdstr("Corrupted Level Data")) {
                    pop = true;
                }

                if (pop && instr.OpCode == OpCodes.Throw) {
                    instr.OpCode = OpCodes.Pop;
                    pop = false;
                    corruptedLevelDataFound = true;
                }

                if (instr.MatchCall("Celeste.BinaryPacker", "FromBinary")) {
                    instri++;

                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Call, m_Process));
                    binaryPackerFound = true;
                }

                if (instri > 2 &&
                    instrs[instri - 3].MatchLdfld("Celeste.ModeProperties", "StrawberriesByCheckpoint") &&
                    instr.MatchCallOrCallvirt("Celeste.EntityData[0...,0...]", "Celeste.EntityData Get(System.Int32,System.Int32)")
                ) {
                    instrs[instri - 3].OpCode = OpCodes.Ldflda;
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_GrowAndGet;
                    instri++;
                    strawberriesByCheckpointFound = true;
                }
            }

            if (!corruptedLevelDataFound) {
                throw new Exception("\"Corrupted Level Data\" not found in " + method.FullName + "!");
            }
            if (!binaryPackerFound) {
                throw new Exception("No call to BinaryPacker.FromBinary found in " + method.FullName + "!");
            }
            if (!strawberriesByCheckpointFound) {
                throw new Exception("No call to StrawberriesByCheckpoint found in " + method.FullName + "!");
            }
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the Godzilla-sized backdrop parsing method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchBackdropParser))]
    class PatchBackdropParserAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchBackdropParser(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_LoadCustomBackdrop = context.Method.DeclaringType.FindMethod("Celeste.Backdrop LoadCustomBackdrop(Celeste.BinaryPacker/Element,Celeste.BinaryPacker/Element,Celeste.MapData)");
            MethodDefinition m_ParseTags = context.Method.DeclaringType.FindMethod("System.Void ParseTags(Celeste.BinaryPacker/Element,Celeste.Backdrop)");

            ILCursor cursor = new ILCursor(context);
            // Remove soon-to-be-unneeded instructions
            cursor.RemoveRange(2);

            // Load custom backdrop at the beginning of the method.
            // If it's been loaded, skip to backdrop setup.
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldarg_2);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Call, m_LoadCustomBackdrop);
            cursor.Emit(OpCodes.Stloc_0);
            cursor.Emit(OpCodes.Ldloc_0);

            // Get the branch target for if a custom backdrop is found
            cursor.FindNext(out ILCursor[] cursors, instr => instr.MatchLdstr("tag"));
            Instruction branchCustomToSetup = cursors[0].Prev;
            cursor.Emit(OpCodes.Brtrue, branchCustomToSetup);

            // Allow multiple comma separated tags
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("tag"), instr => instr.MatchCallvirt("Celeste.BinaryPacker/Element", "HasAttr"))) {
                cursor.Index++; // move past the branch
                cursor.RemoveRange(8); // remove old code

                cursor.Emit(matches switch {
                    0 => OpCodes.Ldarg_1, // child
                    1 => OpCodes.Ldarg_2, // above
                    _ => throw new Exception($"Too many matches for HasAttr(\"tag\"): {matches}")
                }); // child
                cursor.Emit(OpCodes.Ldloc_0); // backdrop

                cursor.Emit(OpCodes.Call, m_ParseTags);

                matches++;
            }
            if (matches != 2) {
                throw new Exception($"Too few matches for HasAttr(\"tag\"): {matches}");
            }
        }

    }
}
