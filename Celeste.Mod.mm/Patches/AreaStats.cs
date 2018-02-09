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
using System.Xml.Serialization;

namespace Celeste {
    class patch_AreaStats : AreaStats {

        [XmlAttribute]
        [MonoModHook("System.Int32 Celeste.AreaStats::ID_Unsafe")]
        public new int ID;

        [MonoModRemove]
        public int ID_Unsafe;

        [XmlIgnore]
        [MonoModHook("System.Int32 Celeste.AreaStats::ID")]
        public int ID_Safe {
            get {
                if (!string.IsNullOrEmpty(SID))
                    return AreaDataExt.Get(SID)?.ID ?? ID_Unsafe;
                return ID_Unsafe;
            }
            set {
                ID_Unsafe = value;
                if (ID_Unsafe != -1)
                    SID = AreaData.Areas[ID_Unsafe].GetSID();
            }
        }

        [XmlAttribute]
        public string SID;

        public string LevelSet {
            get {
                string sid = SID;
                if (string.IsNullOrEmpty(sid))
                    return "";
                int lastIndexOfSlash = sid.LastIndexOf('/');
                if (lastIndexOfSlash == -1)
                    return "";
                return sid.Substring(0, lastIndexOfSlash);
            }
        }

        public patch_AreaStats(int id)
            : base(id) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // The original method: 
        // - doesn't check if the area actually has the mode
        // - iterates by values of AreaMode
        [MonoModReplace]
        public new void CleanCheckpoints() {
            AreaData area = AreaData.Get(ID);

            for (int i = 0; i < Modes.Length; i++) {
                AreaMode areaMode = (AreaMode) i;
                AreaModeStats areaModeStats = Modes[i];
                ModeProperties modeProperties = null;
                if (area.HasMode(areaMode))
                    modeProperties = area.Mode[i];

                HashSet<string> checkpoints = new HashSet<string>(areaModeStats.Checkpoints);
                areaModeStats.Checkpoints.Clear();

                if (modeProperties != null && modeProperties.Checkpoints != null)
                    foreach (CheckpointData checkpointData in modeProperties.Checkpoints)
                        if (checkpoints.Contains(checkpointData.Level))
                            areaModeStats.Checkpoints.Add(checkpointData.Level);
            }
        }

    }
    public static class AreaStatsExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static AreaKey ToKey(this AreaStats self, AreaMode mode)
            => new AreaKey(self.ID, mode).SetSID(self.GetSID());

        public static string GetLevelSet(this AreaStats self)
            => ((patch_AreaStats) self).LevelSet;

        public static string GetSID(this AreaStats self)
            => ((patch_AreaStats) self).SID;
        public static AreaStats SetSID(this AreaStats self, string value) {
            ((patch_AreaStats) self).SID = value;
            return self;
        }

    }
}
