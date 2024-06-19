#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Celeste {
    public class patch_AreaData : AreaData {

#pragma warning disable CS0108 // Hides inherited member

        // Required to reference this class in other files
        public static List<patch_AreaData> Areas;
        public patch_ModeProperties[] Mode;

# pragma warning restore CS0108

        private static Regex ParseNameRegex = new Regex(@"^(?:(?<order>\d+)(?<side>[ABCHX]?)\-)?(?<name>.+?)(?:\-(?<sideAlt>[ABCHX]?))?$", RegexOptions.Compiled);
        private static Dictionary<string, Match> ParseNameCache = new Dictionary<string, Match>();
        private static Dictionary<string, AreaMode> ParseNameModes = new Dictionary<string, AreaMode>() {
            { "A", AreaMode.Normal },
            { "B", AreaMode.BSide },
            { "H", AreaMode.BSide },
            { "C", AreaMode.CSide },
            { "X", AreaMode.CSide },
        };
        private static void ParseName(string sid, out int? order, out AreaMode side, out string name) {
            int indexOfSlash = sid.Replace('\\', '/').LastIndexOf('/');
            if (indexOfSlash != -1)
                sid = sid.Substring(indexOfSlash + 1);
            if (sid.EndsWith(".bin"))
                sid = sid.Substring(0, sid.Length - 4);

            Match match;
            if (!ParseNameCache.TryGetValue(sid, out match))
                ParseNameCache[sid] = match = ParseNameRegex.Match(sid);

            string rawOrder = match.Groups["order"].Value;
            string rawSide = match.Groups["side"].Value;
            if (string.IsNullOrEmpty(rawSide))
                rawSide = match.Groups["sideAlt"].Value;
            string rawName = match.Groups["name"].Value;

            // Special case: 1-B, where 1 is order but should be name, and B is name but should be side
            if (!string.IsNullOrEmpty(rawOrder) && !string.IsNullOrEmpty(rawName) &&
                string.IsNullOrEmpty(rawSide) && ParseNameModes.ContainsKey(rawName)) {
                rawSide = rawName;
                rawName = rawOrder;
                rawOrder = null;
            }

            if (int.TryParse(rawOrder, out int orderTmp))
                order = orderTmp;
            else
                order = null;

            if (!ParseNameModes.TryGetValue(rawSide, out side))
                side = AreaMode.Normal;

            name = rawName;
        }

        /// <summary>
        /// The SID (string ID) of the area.
        /// </summary>
        public string SID;

        private string _LevelSet;
        private WeakReference _LevelSetSID;
        public string LevelSet {
            get {
                string sid = SID;

                if (ReferenceEquals(sid, _LevelSetSID?.Target))
                    return _LevelSet;
                _LevelSetSID = new WeakReference(sid);

                if (string.IsNullOrEmpty(sid))
                    return _LevelSet = "Celeste";

                int lastIndexOfSlash = sid.LastIndexOf('/');
                if (lastIndexOfSlash == -1)
                    return _LevelSet = "";

                return _LevelSet = sid.Substring(0, lastIndexOfSlash);
            }
        }

        // Used for Override A-Side Meta, Only back up useful data.
        public AreaData ASideAreaDataBackup;

        public MapMeta Meta;

        // Required for the journal to hide areas outside of the current levelset.

        [MonoModLinkFrom("System.Boolean Celeste.AreaData::Interlude_Unsafe")]
        public new bool Interlude;

        [MonoModRemove]
        public bool Interlude_Unsafe;

        [MonoModLinkFrom("System.Boolean Celeste.AreaData::Interlude")]
        public bool Interlude_Safe {
            get {
                return
                    Interlude_Unsafe ||
                    (SaveData.Instance != null && patch_SaveData.Instance.LevelSet != LevelSet);
            }
            set {
                Interlude_Unsafe = value;
            }
        }

        [MonoModLinkFrom("System.Boolean Celeste.AreaData::IsFinal_Unsafe")]
        public new bool IsFinal;

        [MonoModRemove]
        public bool IsFinal_Unsafe;

        [MonoModLinkFrom("System.Boolean Celeste.AreaData::IsFinal")]
        public bool IsFinal_Safe {
            get {
                return
                    IsFinal_Unsafe &&
                    (SaveData.Instance != null && patch_SaveData.Instance.LevelSet == LevelSet);
            }
            set {
                IsFinal_Unsafe = value;
            }
        }

        [MonoModReplace]
        public static new patch_AreaData Get(Scene scene) {
            patch_AreaData result;
            if (scene != null && scene is Level) {
                result = Get(((Level) scene).Session.Area);
            } else {
                result = null;
            }
            return result;
        }

        [MonoModReplace]
        public static new patch_AreaData Get(Session session) {
            patch_AreaData result;
            if (session != null) {
                result = Get(session.Area);
            } else {
                result = null;
            }
            return result;
        }

        [MonoModReplace]
        public static new patch_AreaData Get(AreaKey area) {
            if (area.GetSID() == null)
                return Get(area.ID);
            return Get(area.GetSID());
        }

        [MonoModReplace]
        public static new patch_AreaData Get(int id) {
            if (id < 0)
                return null;

            lock (AssetReloadHelper.AreaReloadLock) {
                return Areas[id];
            }
        }

        public static patch_AreaData Get(patch_AreaStats stats) {
            if (stats.SID == null)
                return Get(stats.ID);
            return Get(stats.SID);
        }

        public static patch_AreaData Get(string sid) {
            lock (AssetReloadHelper.AreaReloadLock) {
                return string.IsNullOrEmpty(sid) ? null : Areas.Find(area => area.SID == sid);
            }
        }

        public static extern void orig_Load();
        public static new void Load() {
            orig_Load();

            // assign SIDs and CheckpointData.Area for vanilla maps.
            foreach (patch_AreaData area in Areas) {
                area.SID = "Celeste/" + area.Mode[0].Path;

                for (int modeId = 0; modeId < area.Mode.Length; modeId++) {
                    ModeProperties mode = area.Mode[modeId];
                    if (mode?.Checkpoints == null)
                        continue;

                    foreach (patch_CheckpointData checkpoint in mode.Checkpoints) {
                        checkpoint.Area = area.ToKey((AreaMode) modeId);
                    }
                }
            }

            // Separate array as we sort it afterwards.
            List<patch_AreaData> modAreas = new List<patch_AreaData>();

            lock (Everest.Content.Map) {
                foreach (ModAsset asset in Everest.Content.Map.Values.Where(asset => asset.Type == typeof(AssetTypeMap))) {
                    string path = asset.PathVirtual.Substring(5);

                    patch_AreaData area = new patch_AreaData();

                    // Default values.

                    area.SID = path;
                    area.Name = path;
                    area.Icon = "areas/" + path.ToLowerInvariant();
                    if (!GFX.Gui.Has(area.Icon))
                        area.Icon = "areas/null";

                    area.Interlude = false;
                    area.CanFullClear = true;

                    area.TitleBaseColor = Calc.HexToColor("6c7c81");
                    area.TitleAccentColor = Calc.HexToColor("2f344b");
                    area.TitleTextColor = Color.White;

                    area.IntroType = Player.IntroTypes.WakeUp;

                    area.Dreaming = false;
                    area.ColorGrade = null;

                    area.Mode = new patch_ModeProperties[] {
                        new patch_ModeProperties {
                            Inventory = PlayerInventory.Default,
                            AudioState = new AudioState(SFX.music_city, SFX.env_amb_00_main)
                        }
                    };

                    area.Wipe = (Scene scene, bool wipeIn, Action onComplete)
                        => new AngledWipe(scene, wipeIn, onComplete);

                    area.DarknessAlpha = 0.05f;
                    area.BloomBase = 0f;
                    area.BloomStrength = 1f;

                    area.Jumpthru = "wood";

                    area.CassseteNoteColor = Calc.HexToColor("33a9ee");
                    area.CassetteSong = SFX.cas_01_forsaken_city;

                    if (string.IsNullOrEmpty(area.Mode[0].Path))
                        area.Mode[0].Path = asset.PathVirtual.Substring(5);

                    // Some of the game's code checks for [1] / [2] explicitly.
                    // Let's just provide null modes to fill any gaps.
                    if (area.Mode.Length < 3) {
                        patch_ModeProperties[] larger = new patch_ModeProperties[3];
                        for (int i = 0; i < area.Mode.Length; i++)
                            larger[i] = area.Mode[i];
                        area.Mode = larger;
                    }

                    // Celeste levelset always appears first.
                    if (area.LevelSet == "Celeste")
                        Areas.Add(area);
                    else
                        modAreas.Add(area);

                    // Some special handling.
                    area.OnLevelBegin = (level) => {
                        MapMetaModeProperties levelMetaMode = ((patch_MapData) level.Session.MapData).Meta;

                        if (levelMetaMode?.SeekerSlowdown ?? false)
                            level.Add(new SeekerEffectsController());
                    };
                }
            }


            // Merge modAreas into Areas.
            Areas.AddRange(modAreas);

            // Find duplicates and remove any earlier copies.
            for (int i = 0; i < Areas.Count; i++) {
                patch_AreaData area = Areas[i];
                int otherIndex = Areas.FindIndex(other => other.SID == area.SID);
                if (otherIndex < i) {
                    Areas[otherIndex] = area;
                    Areas.RemoveAt(i);
                    i--;
                }
            }

            // Sort areas.
            Areas.Sort(AreaComparison);

            // Remove AreaDatas which are now a mode of another AreaData.
            // This can happen late as the map data (.bin) can contain additional metadata.
            for (int i = 0; i < Areas.Count; i++) {
                patch_AreaData area = Areas[i];
                string path = area.Mode[0].Path;
                int otherIndex = Areas.FindIndex(other => other.Mode.Any(otherMode => otherMode?.Path == path));
                if (otherIndex != -1 && otherIndex != i) {
                    Areas.RemoveAt(i);
                    i--;
                    continue;
                }

                ParseName(path, out int? order, out AreaMode side, out string name);

                // Also check for .bins possibly belonging to A side .bins by their path and lack of existing modes.
                for (int ii = 0; ii < Areas.Count; ii++) {
                    patch_AreaData other = Areas[ii];
                    ParseName(other.Mode[0].Path, out int? otherOrder, out AreaMode otherSide, out string otherName);

                    if (area.LevelSet == other.LevelSet && order == otherOrder && name == otherName && side != otherSide &&
                        !other.HasMode(side)) {
                        if (other.Mode[(int) side] == null)
                            other.Mode[(int) side] = new patch_ModeProperties {
                                Inventory = PlayerInventory.Default,
                                AudioState = new AudioState(SFX.music_city, SFX.env_amb_00_main)
                            };
                        other.Mode[(int) side].Path = path;
                        Areas.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }

            for (int i = 0; i < Areas.Count; i++) {
                patch_AreaData area = Areas[i];
                area.ID = i;

                // Clean up non-existing modes.
                int modei = 0;
                for (; modei < area.Mode.Length; modei++) {
                    ModeProperties mode = area.Mode[modei];
                    if (mode == null || string.IsNullOrEmpty(mode.Path))
                        break;
                }
                Array.Resize(ref area.Mode, modei);

                Logger.Log(LogLevel.Verbose, "AreaData", string.Format("{0}: {1} - {2} sides", i, area.SID, area.Mode.Length));

                // Update old MapData areas and load any new areas.

                // Add the A side MapData or update its area key.
                if (area.Mode[0].MapData != null)
                    area.Mode[0].MapData.Area = area.ToKey();
                else
                    area.Mode[0].MapData = new patch_MapData(area.ToKey());

                if (area.IsInterludeUnsafe())
                    continue;

                // A and (some) B sides have PoemIDs. Can be overridden via empty PoemID.
                if (area.Mode[0].PoemID == null)
                    area.Mode[0].PoemID = area.SID.DialogKeyify() + "_A";
                if (area.Mode.Length > 1 &&
                    area.Mode[1] != null &&
                    area.Mode[1].PoemID == null) {
                    area.Mode[1].PoemID = area.SID.DialogKeyify() + "_B";
                }

                // Update all other existing mode's area keys.
                for (int mode = 1; mode < area.Mode.Length; mode++) {
                    if (area.Mode[mode] == null)
                        continue;
                    if (area.Mode[mode].MapData != null)
                        area.Mode[mode].MapData.Area = area.ToKey((AreaMode) mode);
                    else
                        area.Mode[mode].MapData = new patch_MapData(area.ToKey((AreaMode) mode));
                }
            }

            // Load custom mountains
            // This needs to be done after areas are loaded because it depends on the MapMeta
            MTNExt.LoadMod();
            MTNExt.LoadModData();
        }

        private static int AreaComparison(patch_AreaData a, patch_AreaData b) {
            string aSet = a.LevelSet;
            string aSID = a.SID;
            MapMeta aMeta = a.Meta;
            string bSet = b.LevelSet;
            string bSID = b.SID;
            MapMeta bMeta = b.Meta;

            // Celeste appears before everything else.
            if (aSet == "Celeste" && bSet != "Celeste")
                return -1;
            if (aSet != "Celeste" && bSet == "Celeste")
                return 1;

            // Uncategorized appears after everything else.
            if (string.IsNullOrEmpty(aSet) && !string.IsNullOrEmpty(bSet))
                return 1;
            if (!string.IsNullOrEmpty(aSet) && string.IsNullOrEmpty(bSet))
                return -1;

            // Compare level sets alphabetically.
            if (aSet != bSet)
                return string.Compare(aSet, bSet);

            // Put "parented" levels at the end.
            if (!string.IsNullOrEmpty(aMeta?.Parent) && string.IsNullOrEmpty(bMeta?.Parent))
                return 1;
            if (string.IsNullOrEmpty(aMeta?.Parent) && !string.IsNullOrEmpty(bMeta?.Parent))
                return -1;

            ParseName(aSID, out int? aOrder, out AreaMode aSide, out string aName);

            ParseName(bSID, out int? bOrder, out AreaMode bSide, out string bName);

            // put the "unordered" levels at the end. (Farewell is one of them.)
            if (aOrder != null && bOrder == null)
                return -1;

            if (aOrder == null && bOrder != null)
                return 1;

            // order the rest by order, then by name, then by side
            if (aOrder != null && bOrder != null && aOrder.Value != bOrder.Value)
                return aOrder.Value - bOrder.Value;

            if (aName != bName)
                return string.Compare(aName, bName);

            if (aSide != bSide)
                return aSide - bSide;

            // everything is the same: this is the same level
            return 0;
        }

        public static string GetStartName(AreaKey area) {
            string start_key = string.Format("{0}/{1}/start", area.GetSID(), (char) ('A' + (int) area.Mode));
            if (patch_AreaData.Get(area).LevelSet == "Celeste" || !Dialog.Has(start_key))
                return Dialog.Clean("overworld_start");
            return Dialog.Clean(start_key);
        }

        public static extern string orig_GetCheckpointName(AreaKey area, string level);
        public static new string GetCheckpointName(AreaKey area, string level) {
            int split = level?.IndexOf('|') ?? -1;
            if (split >= 0) {
                area = Get(level.Substring(0, split))?.ToKey(area.Mode) ?? area;
                level = level.Substring(split + 1);
            }
            return orig_GetCheckpointName(area, level);
        }

    }

    [Obsolete("Use AreaData members instead.")]
    public static class AreaDataExt {

        [Obsolete("Use AreaData.Get(AreaStats) instead.")]
        public static AreaData Get(patch_AreaStats stats)
            => patch_AreaData.Get(stats);
        [Obsolete("Use AreaData.Get(string) instead.")]
        public static AreaData Get(string sid)
            => patch_AreaData.Get(sid);

        /// <summary>
        /// Check if the AreaData is an interlude (like Prologue and Epilogue).
        /// </summary>
        public static bool IsInterludeUnsafe(this patch_AreaData self)
            => self.Interlude_Unsafe;

        /// <summary>
        /// Get an AreaKey for this area.
        /// </summary>
        public static AreaKey ToKey(this patch_AreaData self, AreaMode mode = AreaMode.Normal)
            => new AreaKey(self.ID, mode).SetSID(self.SID);

        /// <summary>
        /// Get the name of the level set this area belongs to.
        /// </summary>
        [Obsolete("Use AreaData.LevelSet instead.")]
        public static string GetLevelSet(this patch_AreaData self)
            => self.LevelSet;

        /// <summary>
        /// Check if the area is official.
        /// </summary>
        public static bool IsOfficialLevelSet(this patch_AreaData self)
            => self.LevelSet == "Celeste";

        /// <summary>
        /// Get the SID (string ID) of the area.
        /// </summary>
        [Obsolete("Use AreaData.SID instead.")]
        public static string GetSID(this patch_AreaData self)
            => self.SID;
        /// <summary>
        /// Set the SID (string ID) of the area.
        /// </summary>
        [Obsolete("Use AreaData.SID instead.")]
        public static AreaData SetSID(this patch_AreaData self, string value) {
            self.SID = value;
            return self;
        }

        /// <summary>
        /// Get the custom metadata if it has been loaded from the .meta or set otherwise.
        /// </summary>
        [Obsolete("Use AreaData.Meta instead.")]
        public static MapMeta GetMeta(this patch_AreaData self)
            => self.Meta;
        /// <summary>
        /// Set the custom metadata.
        /// </summary>
        [Obsolete("Use AreaData.Meta instead.")]
        public static AreaData SetMeta(this patch_AreaData self, MapMeta value) {
            self.Meta = value;
            return self;
        }

        /// <summary>
        /// Get the A-Side's area data backup.
        /// </summary>
        [Obsolete("Use AreaData.ASideAreaDataBackup instead.")]
        public static AreaData GetASideAreaDataBackup(this patch_AreaData self)
            => self.ASideAreaDataBackup;

        /// <summary>
        /// Set the A-Side's area data backup.
        /// </summary>
        [Obsolete("Use AreaData.ASideAreaDataBackup instead.")]
        public static AreaData SetASideAreaDataBackup(this patch_AreaData self, AreaData value) {
            self.ASideAreaDataBackup = value;
            return self;
        }

        /// <summary>
        /// Restore A-Side's area data from backup.
        /// </summary>
        public static void RestoreASideAreaData(this patch_AreaData self) {
            AreaData backup = self.ASideAreaDataBackup;
            if (backup == null)
                return;

            self.IntroType = backup.IntroType;
            self.ColorGrade = backup.ColorGrade;
            self.DarknessAlpha = backup.DarknessAlpha;
            self.BloomBase = backup.BloomBase;
            self.BloomStrength = backup.BloomStrength;
            self.CoreMode = backup.CoreMode;
            self.Dreaming = backup.Dreaming;
        }

        /// <summary>
        /// Get the custom metadata of the mode if OverrideASideMeta is enabled. 
        /// </summary>
        public static MapMeta GetModeMeta(this patch_AreaData self, AreaMode value) {
            if (self.Mode[(int) value]?.GetMapMeta() is MapMeta mapMeta) {
                if (value != AreaMode.Normal && (mapMeta.OverrideASideMeta ?? false))
                    return mapMeta;
            }

            return self.Meta;
        }

        /// <summary>
        /// Apply the metadata of the mode to the area if OverrideASideMeta is enabled.
        /// </summary>
        public static void OverrideASideMeta(this patch_AreaData self, AreaMode value) {
            if (self.LevelSet == "Celeste")
                return;

            if (value == AreaMode.Normal)
                return;

            if (!(self.Mode[(int) value]?.GetMapMeta() is MapMeta mapMeta))
                return;

            if (!(mapMeta.OverrideASideMeta ?? false))
                return;

            mapMeta.ApplyToForOverride(self);
        }
    }
}
