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

        [XmlAttribute]
        public string Name;

        public int UnlockedAreas;

        public List<AreaStats> Areas = new List<AreaStats>();

        public int AreaOffset {
            get {
                return AreaData.Areas.FindIndex(area => area.GetLevelSet() == Name);
            }
        }

        public int MaxArea {
            get {
                if (Celeste.PlayMode == Celeste.PlayModes.Event)
                    return AreaOffset + 2;
                return AreaData.Areas.Count(area => area.GetLevelSet() == Name) - 1;
            }
        }

    }
    class patch_SaveData : SaveData {

        public List<LevelSetStats> LevelSets = new List<LevelSetStats>();

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
                List<AreaStats> areas = set.Areas;
                if (set.Name == "Celeste")
                    areas = Areas_Unsafe;

                int count = AreaData.Areas.Count(other => other.GetLevelSet() == set.Name);
                while (areas.Count < count) {
                    areas.Add(new AreaStats(areas.Count));
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

    }
}
