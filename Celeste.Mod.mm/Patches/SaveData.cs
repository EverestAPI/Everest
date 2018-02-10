#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Celeste {
    [Serializable]
    public class LevelSetStats {

        internal patch_SaveData SaveData;

        [XmlAttribute]
        public string Name;

        public int UnlockedAreas;

        public List<AreaStats> Areas = new List<AreaStats>();
        [XmlIgnore]
        public List<AreaStats> AreasIncludingCeleste => Name == "Celeste" ? SaveData.Areas_Unsafe : Areas;

        public int TotalStrawberries;

        [XmlIgnore]
        public int AreaOffset {
            get {
                return AreaData.Areas.FindIndex(area => area.GetLevelSet() == Name);
            }
        }

        [XmlIgnore]
        public int UnlockedModes {
            get {
                if (TotalHeartGems >= 16) {
                    return 3;
                }

                int offset = AreaOffset;
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
                return AreasIncludingCeleste.Count(area => area.Modes.Any(mode => mode?.HeartGem ?? false));
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
    class patch_SaveData : SaveData {

        public List<LevelSetStats> LevelSets = new List<LevelSetStats>();

        [XmlIgnore]
        public string LevelSet => LastArea.GetLevelSet() ?? "Celeste";

        [XmlIgnore]
        public LevelSetStats LevelSetStats {
            get {
                LevelSetStats set = LevelSets.Find(other => other.Name == LevelSet);
                if (set != null)
                    return set;

                set = new LevelSetStats {
                    Name = LevelSet,
                    UnlockedAreas = 0
                };
                LevelSets.Add(set);
                return set;
            }
        }

        [XmlAttribute]
        [MonoModHook("System.Int32 Celeste.SaveData::UnlockedAreas_Unsafe")]
        public new int UnlockedAreas;

        [MonoModRemove]
        public int UnlockedAreas_Unsafe;

        [XmlIgnore]
        [MonoModHook("System.Int32 Celeste.SaveData::UnlockedAreas")]
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
        [MonoModHook("System.Int32 Celeste.SaveData::TotalStrawberries_Unsafe")]
        public new int TotalStrawberries;

        [MonoModRemove]
        public int TotalStrawberries_Unsafe;

        [XmlIgnore]
        [MonoModHook("System.Int32 Celeste.SaveData::TotalStrawberries")]
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
        [MonoModHook("System.Collections.Generic.List`1<Celeste.AreaStats> Celeste.SaveData::Areas_Unsafe")]
        public new List<AreaStats> Areas;

        [MonoModRemove]
        public List<AreaStats> Areas_Unsafe;

        [XmlIgnore]
        [MonoModHook("System.Collections.Generic.List`1<Celeste.AreaStats> Celeste.SaveData::Areas")]
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

        [MonoModReplace]
        public new void AfterInitialize() {
            if (LevelSets == null)
                LevelSets = new List<LevelSetStats>();

            foreach (AreaData area in AreaData.Areas) {
                string set = area.GetLevelSet();
                if (!LevelSets.Exists(other => other.Name == set)) {
                    LevelSets.Add(new LevelSetStats {
                        Name = set,
                        UnlockedAreas = set == "Celeste" ? UnlockedAreas_Unsafe : 0
                    });
                }
            }

            foreach (LevelSetStats set in LevelSets) {
                set.SaveData = this;
                List<AreaStats> areas = set.Areas;
                if (set.Name == "Celeste")
                    areas = Areas_Unsafe;

                int offset = set.AreaOffset;
                int count = AreaData.Areas.Count(other => other.GetLevelSet() == set.Name);
                while (areas.Count < count) {
                    areas.Add(new AreaStats(offset + areas.Count));
                }
                while (areas.Count > AreaData.Areas.Count) {
                    areas.RemoveAt(areas.Count - 1);
                }

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


            if (DebugMode) {
                CurrentSession = null;
            }

            if (string.IsNullOrEmpty(TheoSisterName)) {
                TheoSisterName = Dialog.Clean("THEO_SISTER_NAME", null);
                if (Name.IndexOf(TheoSisterName, StringComparison.InvariantCultureIgnoreCase) >= 0) {
                    TheoSisterName = Dialog.Clean("THEO_SISTER_ALT_NAME", null);
                }
            }

            if (!AssistMode) {
                Assists = default(Assists);
            }

            if (Assists.GameSpeed < 5 || Assists.GameSpeed > 10) {
                Assists.GameSpeed = 10;
            }

            Everest.Invoke("LoadSaveData", FileSlot);
        }

        public extern void orig_BeforeSave();
        public new void BeforeSave() {
            orig_BeforeSave();
            Everest.Invoke("SaveSaveData", FileSlot);
        }

        public LevelSetStats GetLevelSetStatsFor(string name)
            => LevelSets.Find(set => set.Name == name);

    }
    public static class SaveDataExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static List<LevelSetStats> GetLevelSets(this SaveData self)
            => ((patch_SaveData) self).LevelSets;
        public static SaveData SetLevelSets(this SaveData self, List<LevelSetStats> value) {
            ((patch_SaveData) self).LevelSets = value;
            return self;
        }

        public static string GetLevelSet(this SaveData self)
            => ((patch_SaveData) self).LevelSet;

        public static LevelSetStats GetLevelSetStats(this SaveData self)
            => ((patch_SaveData) self).LevelSetStats;

        public static LevelSetStats GetLevelSetStatsFor(this SaveData self, string name)
            => ((patch_SaveData) self).GetLevelSetStatsFor(name);

    }
}
