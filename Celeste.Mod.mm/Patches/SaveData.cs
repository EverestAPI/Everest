#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Celeste {
    class patch_SaveData : SaveData {

        public List<LevelSetStats> LevelSets = new List<LevelSetStats>();

        [XmlIgnore]
        public string LevelSet => LevelSetStats.Name;

        [XmlIgnore]
        public LevelSetStats LevelSetStats {
            get {
                string name = LastArea.GetLevelSet() ?? "Celeste";
                LevelSetStats set = LevelSets.Find(other => other.Name == name);

                if (set == null) {
                    // Just silently add the missing levelset.
                    LevelSets.Add(set = new LevelSetStats {
                        SaveData = this,
                        Name = name,
                        UnlockedAreas = 0
                    });
                }

                // If the levelset doesn't exist in AreaData.Areas anymore (offset == -1), fall back.
                if (name != "Celeste" && set.AreaOffset == -1) {
                    LastArea = AreaKey.Default;
                    // Recurse - get the new, proper level set.
                    return LevelSetStats;
                }

                return set;
            }
        }

        // We want use LastArea_Safe instead of LastArea to avoid breaking vanilla Celeste.

        [MonoModLinkFrom("Celeste.AreaKey Celeste.SaveData::LastArea_Unsafe")]
        public new AreaKey LastArea;

        [MonoModRemove]
        public AreaKey LastArea_Unsafe;

        [MonoModLinkFrom("Celeste.AreaKey Celeste.SaveData::LastArea")]
        public AreaKey LastArea_Safe;

        // We want use CurrentSession_Safe instead of CurrentSession to avoid breaking vanilla Celeste.

        [MonoModLinkFrom("Celeste.Session Celeste.SaveData::CurrentSession_Unsafe")]
        public new Session CurrentSession;

        [MonoModRemove]
        public Session CurrentSession_Unsafe;

        [MonoModLinkFrom("Celeste.Session Celeste.SaveData::CurrentSession")]
        public Session CurrentSession_Safe;

        // Legacy code should benefit from the new LevelSetStats.

        [XmlAttribute]
        [MonoModLinkFrom("System.Int32 Celeste.SaveData::UnlockedAreas_Unsafe")]
        public new int UnlockedAreas;

        [MonoModRemove]
        public int UnlockedAreas_Unsafe;

        [XmlIgnore]
        [MonoModLinkFrom("System.Int32 Celeste.SaveData::UnlockedAreas")]
        public int UnlockedAreas_Safe {
            get {
                if (LevelSet == "Celeste")
                    return UnlockedAreas_Unsafe;
                return LevelSetStats.AreaOffset + LevelSetStats.UnlockedAreas;
            }
            set {
                if (LevelSets == null || LevelSet == "Celeste") {
                    UnlockedAreas_Unsafe = value;
                    return;
                }
                LevelSetStats.UnlockedAreas = value - LevelSetStats.AreaOffset;
            }
        }


        [XmlAttribute]
        [MonoModLinkFrom("System.Int32 Celeste.SaveData::TotalStrawberries_Unsafe")]
        public new int TotalStrawberries;

        [MonoModRemove]
        public int TotalStrawberries_Unsafe;

        [XmlIgnore]
        [MonoModLinkFrom("System.Int32 Celeste.SaveData::TotalStrawberries")]
        public int TotalStrawberries_Safe {
            get {
                if (LevelSet == "Celeste")
                    return TotalStrawberries_Unsafe;
                return LevelSetStats.TotalStrawberries;
            }
            set {
                if (LevelSets == null || LevelSet == "Celeste") {
                    TotalStrawberries_Unsafe = value;
                    return;
                }
                LevelSetStats.TotalStrawberries = value;
            }
        }


        [XmlAttribute]
        [MonoModLinkFrom("System.Collections.Generic.List`1<Celeste.AreaStats> Celeste.SaveData::Areas_Unsafe")]
        public new List<AreaStats> Areas;

        [MonoModRemove]
        public List<AreaStats> Areas_Unsafe;

        [XmlIgnore]
        [MonoModLinkFrom("System.Collections.Generic.List`1<Celeste.AreaStats> Celeste.SaveData::Areas")]
        public List<AreaStats> Areas_Safe {
            get {
                List<AreaStats> areasAll = new List<AreaStats>(Areas_Unsafe);
                foreach (LevelSetStats set in LevelSets) {
                    areasAll.AddRange(set.Areas);
                }
                return areasAll;
            }
            set {
                if (LevelSets == null && value.Count == 0) {
                    Areas_Unsafe = value;
                    return;
                }

                int i = 0;
                Areas_Unsafe = value.GetRange(i, Areas_Unsafe.Count);
                i += Areas_Unsafe.Count;
                foreach (LevelSetStats set in LevelSets) {
                    set.Areas = value.GetRange(i, set.Areas.Count);
                    i += set.Areas.Count;
                }
            }
        }

        public new int UnlockedModes {
            [MonoModReplace]
            get {
                if (DebugMode || CheatMode) {
                    return 3;
                }

                return LevelSetStats.UnlockedModes;
            }
        }

        public new int MaxArea {
            [MonoModReplace]
            get {
                return LevelSetStats.AreaOffset + LevelSetStats.MaxArea;
            }
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<System.String> Celeste.SaveData::Poem_Unsafe")]
        public new List<string> Poem;

        [MonoModRemove]
        public List<string> Poem_Unsafe;

        [XmlIgnore]
        [MonoModLinkFrom("System.Collections.Generic.List`1<System.String> Celeste.SaveData::Poem")]
        public List<string> Poem_Safe {
            get {
                if (LevelSet == "Celeste")
                    return Poem_Unsafe;
                return LevelSetStats.Poem;
            }
            set {
                if (LevelSets == null || LevelSet == "Celeste") {
                    Poem_Unsafe = value;
                    return;
                }
                LevelSetStats.Poem = value;
            }
        }

        [MonoModReplace]
        public new void AfterInitialize() {
            // Vanilla / new saves don't have the LevelSets list.
            if (LevelSets == null)
                LevelSets = new List<LevelSetStats>();

            if (Areas_Unsafe == null)
                Areas_Unsafe = new List<AreaStats>();

            // Add missing LevelSetStats.
            foreach (AreaData area in AreaData.Areas) {
                string set = area.GetLevelSet();
                if (!LevelSets.Exists(other => other.Name == set)) {
                    LevelSets.Add(new LevelSetStats {
                        Name = set,
                        UnlockedAreas = set == "Celeste" ? UnlockedAreas_Unsafe : 0
                    });
                }
            }

            // Fill each LevelSetStats with its areas.
            for (int lsi = 0; lsi < LevelSets.Count; lsi++) {
                LevelSetStats set = LevelSets[lsi];
                set.SaveData = this;
                List<AreaStats> areas = set.Areas;
                if (set.Name == "Celeste")
                    areas = Areas_Unsafe;

                int offset = set.AreaOffset;
                if (offset == -1) {
                    // LevelSet gone - let's remove it to prevent any unwanted accesses.
                    // We previously kept the LevelSetStats around in case the levelset resurfaces later on, but as it turns out, this breaks some stuff.
                    LevelSets.RemoveAt(lsi);
                    lsi--;
                    continue;
                }

                // Refresh all stat IDs based on their SIDs, sort, fill and remove leftovers.
                // Temporarily use ID_Unsafe; later ID_Safe to ID_Unsafe to resync the SIDs.
                // This keeps the stats bound to their SIDs, not their indices, while removing non-existent areas.
                int count = AreaData.Areas.Count(other => other.GetLevelSet() == set.Name);
                // Fix IDs
                for (int i = 0; i < areas.Count; i++)
                    ((patch_AreaStats) areas[i]).ID_Unsafe = AreaDataExt.Get(areas[i])?.ID ?? int.MaxValue;
                // Sort
                areas.Sort((a, b) => ((patch_AreaStats) a).ID_Unsafe - ((patch_AreaStats) b).ID_Unsafe);
                // Remove leftovers
                while (areas.Count > 0 && ((patch_AreaStats) areas[areas.Count - 1]).ID_Unsafe == int.MaxValue)
                    areas.RemoveAt(areas.Count - 1);
                // Fill gaps
                for (int i = 0; i < count; i++)
                    if (i >= areas.Count || ((patch_AreaStats) areas[i]).ID_Unsafe != offset + i)
                        areas.Insert(i, new AreaStats(offset + i));
                // Resync SIDs
                for (int i = 0; i < areas.Count; i++)
                    ((patch_AreaStats) areas[i]).ID_Safe = ((patch_AreaStats) areas[i]).ID_Unsafe;

                int lastCompleted = -1;
                for (int i = 0; i < count; i++) {
                    if (areas[i].Modes[0].Completed) {
                        lastCompleted = i;
                    }
                }

                if (set.Name == "Celeste") {
                    if (UnlockedAreas_Unsafe < lastCompleted + 1 && set.MaxArea >= lastCompleted + 1) {
                        UnlockedAreas_Unsafe = lastCompleted + 1;
                    }
                    if (DebugMode) {
                        UnlockedAreas_Unsafe = set.MaxArea;
                    }

                } else {
                    if (set.UnlockedAreas < lastCompleted + 1 && set.MaxArea >= lastCompleted + 1) {
                        set.UnlockedAreas = lastCompleted + 1;
                    }
                    if (DebugMode) {
                        set.UnlockedAreas = set.MaxArea;
                    }
                }

                foreach (AreaStats area in areas) {
                    area.CleanCheckpoints();
                }
            }

            // Order the levelsets to appear just as their areas appear in AreaData.Areas
            LevelSets.Sort((set1, set2) => set1.AreaOffset.CompareTo(set2.AreaOffset));

            // Carry over any progress from vanilla saves.
            if (LastArea_Unsafe.ID != 0)
                LastArea_Safe = LastArea_Unsafe;
            if (CurrentSession_Unsafe != null)
                CurrentSession_Safe = CurrentSession_Unsafe;

            // Trick unmodded instances of Celeste to thinking that we last selected prologue / played no level.
            LastArea_Unsafe = AreaKey.Default;
            CurrentSession_Unsafe = null;

            // Fix out of bounds areas.
            if (LastArea.ID < 0 || LastArea.ID >= AreaData.Areas.Count)
                LastArea = AreaKey.Default;

            // Debug mode shouldn't auto-enter into a level.
            if (DebugMode) {
                CurrentSession = null;
            }

            if (string.IsNullOrEmpty(TheoSisterName)) {
                TheoSisterName = Dialog.Clean("THEO_SISTER_NAME", null);
                if (Name.IndexOf(TheoSisterName, StringComparison.InvariantCultureIgnoreCase) >= 0) {
                    TheoSisterName = Dialog.Clean("THEO_SISTER_ALT_NAME", null);
                }
            }

            AssistModeChecks();

            if (Version != null) {
                Version v = new Version(Version);

                if (v < new Version(1, 2, 1, 1)) {
                    for (int id = 0; id < Areas_Unsafe.Count; id++) {
                        AreaStats area = Areas_Unsafe[id];
                        if (area == null)
                            continue;
                        for (int modei = 0; modei < area.Modes.Length; modei++) {
                            AreaModeStats mode = area.Modes[modei];
                            if (mode == null)
                                continue;
                            if (mode.BestTime > 0L) {
                                mode.SingleRunCompleted = true;
                            }
                            mode.BestTime = 0L;
                            mode.BestFullClearTime = 0L;
                        }
                    }
                }
            }
        }

        [MonoModIfFlag("Fill:AssistModeChecks")]
        public new void AssistModeChecks() {
            if (!AssistMode)
                Assists = default(Assists);

            if (Assists.GameSpeed == 0)
                Assists.GameSpeed = 10;
            if (Assists.GameSpeed < 5 || Assists.GameSpeed > 10)
                Assists.GameSpeed = 10;
        }

        public extern void orig_BeforeSave();
        public new void BeforeSave() {
            // If we're in a Vanilla-compatible area, copy from _Safe (new) to _Unsafe (legacy).
            if (LastArea_Safe.GetLevelSet() == "Celeste")
                LastArea_Unsafe = LastArea_Safe;
            if (CurrentSession_Safe != null && CurrentSession_Safe.Area.GetLevelSet() == "Celeste")
                CurrentSession_Unsafe = CurrentSession_Safe;

            orig_BeforeSave();

            Everest.Invoke("SaveSaveData", FileSlot);
        }

        public LevelSetStats GetLevelSetStatsFor(string name)
            => LevelSets.Find(set => set.Name == name);

        public AreaStats GetAreaStatsFor(AreaKey key)
            => LevelSets.Find(set => set.Name == key.GetLevelSet()).Areas.Find(area => area.GetSID() == key.GetSID());

        public extern HashSet<string> orig_GetCheckpoints(AreaKey area);
        public new HashSet<string> GetCheckpoints(AreaKey area) {
            HashSet<string> checkpoints = orig_GetCheckpoints(area);

            if (Celeste.PlayMode == Celeste.PlayModes.Event ||
                DebugMode || CheatMode) {
                return checkpoints;
            }

            // Remove any checkpoints which don't exist in the level.
            ModeProperties mode = AreaData.Get(area).Mode[(int) area.Mode];
            if (mode == null) {
                checkpoints.Clear();
            } else {
                checkpoints.RemoveWhere(a => !mode.Checkpoints.Any(b => b.Level == a));
            }
            return checkpoints;
        }

    }
    [Serializable]
    public class LevelSetStats {

        internal patch_SaveData SaveData;

        [XmlAttribute]
        public string Name;

        [XmlIgnore]
        [NonSerialized]
        private int _UnlockedAreas;
        public int UnlockedAreas {
            get {
                if (Name == "Celeste" && SaveData != null)
                    return SaveData.UnlockedAreas_Unsafe;
                return Calc.Clamp(_UnlockedAreas, 0, AreasIncludingCeleste?.Count ?? 0);
            }
            set {
                if (Name == "Celeste" && SaveData != null) {
                    SaveData.UnlockedAreas_Unsafe = value;
                    return;
                }
                _UnlockedAreas = value;
            }
        }

        public List<AreaStats> Areas = new List<AreaStats>();
        [XmlIgnore]
        public List<AreaStats> AreasIncludingCeleste => Name == "Celeste" ? SaveData.Areas_Unsafe : Areas;

        public List<string> Poem = new List<string>();

        [XmlIgnore]
        [NonSerialized]
        private int _TotalStrawberries;
        public int TotalStrawberries {
            get {
                // TODO: Dynamically calculate?
                if (Name == "Celeste" && SaveData != null)
                    return SaveData.TotalStrawberries_Unsafe;
                return _TotalStrawberries;
            }
            set {
                if (Name == "Celeste" && SaveData != null) {
                    SaveData.TotalStrawberries_Unsafe = value;
                    return;
                }
                _TotalStrawberries = value;
            }
        }

        [XmlIgnore]
        public int AreaOffset {
            get {
                return AreaData.Areas.FindIndex(area => area.GetLevelSet() == Name);
            }
        }

        [XmlIgnore]
        public int UnlockedModes {
            get {
                int offset = AreaOffset;

                int maxHeartGems;
                if (Name == "Celeste") {
                    maxHeartGems = 16;
                } else {
                    maxHeartGems = 0;
                    for (int i = 0; i <= MaxArea; i++) {
                        if (!AreaData.Areas[offset + i].Interlude) {
                            maxHeartGems += 2;
                        }
                    }
                }

                if (TotalHeartGems >= maxHeartGems) {
                    return 3;
                }

                for (int i = 0; i <= MaxArea; i++) {
                    if (!AreaData.Areas[offset + i].Interlude && AreasIncludingCeleste[i].Cassette) {
                        return 2;
                    }
                }

                return 1;
            }
        }

        [XmlIgnore]
        public int MaxArea {
            get {
                int count = AreaData.Areas.Count(area => area.GetLevelSet() == Name) - 1;
                if (Celeste.PlayMode == Celeste.PlayModes.Event)
                    return Math.Min(count, AreaOffset + 2);
                return count;
            }
        }

        [XmlIgnore]
        public int TotalHeartGems {
            get {
                return AreasIncludingCeleste.Sum(area => area.Modes.Count(mode => mode?.HeartGem ?? false));
            }
        }

        [XmlIgnore]
        public int TotalCassettes {
            get {
                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    if (!AreaData.Areas[offset + i].Interlude && AreasIncludingCeleste[i].Cassette) {
                        count++;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int TotalCompletions {
            get {
                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    if (!AreaData.Areas[offset + i].Interlude && AreasIncludingCeleste[i].Modes[0].Completed) {
                        count++;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int CompletionPercent {
            get {
                // TODO: Get max counts on the fly.
                float value = 0f;
                value += TotalHeartGems / 24f * 24f;
                value += TotalStrawberries / 175f * 55f;
                value += TotalCassettes / 8f * 7f;
                value += TotalCompletions / 8f * 14f;
                return (int) value;
            }
        }

    }
    public static class SaveDataExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the statistics for all level sets.
        /// </summary>
        public static List<LevelSetStats> GetLevelSets(this SaveData self)
            => ((patch_SaveData) self).LevelSets;
        /// <summary>
        /// Set the statistics for all level sets.
        /// </summary>
        public static SaveData SetLevelSets(this SaveData self, List<LevelSetStats> value) {
            ((patch_SaveData) self).LevelSets = value;
            return self;
        }

        /// <summary>
        /// Get the last played level set.
        /// </summary>
        public static string GetLevelSet(this SaveData self)
            => ((patch_SaveData) self).LevelSet;

        /// <summary>
        /// Get the statistics for the last played level set.
        /// </summary>
        public static LevelSetStats GetLevelSetStats(this SaveData self)
            => ((patch_SaveData) self).LevelSetStats;

        /// <summary>
        /// Get the statistics for a given level set.
        /// </summary>
        public static LevelSetStats GetLevelSetStatsFor(this SaveData self, string name)
            => ((patch_SaveData) self).GetLevelSetStatsFor(name);

    }
}
