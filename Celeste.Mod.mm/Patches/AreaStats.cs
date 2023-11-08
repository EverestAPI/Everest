#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using MonoMod;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace Celeste {
    public class patch_AreaStats : AreaStats {

        [XmlAttribute]
        [MonoModLinkFrom("System.Int32 Celeste.AreaStats::ID_Unsafe")]
        public new int ID;

        [MonoModRemove]
        public int ID_Unsafe;

        [XmlIgnore]
        [MonoModLinkFrom("System.Int32 Celeste.AreaStats::ID")]
        public int ID_Safe {
            get {
                if (!string.IsNullOrEmpty(SID))
                    return patch_AreaData.Get(SID)?.ID ?? ID_Unsafe;
                return ID_Unsafe;
            }
            set {
                ID_Unsafe = value;
                if (ID_Unsafe != -1)
                    SID = patch_AreaData.Areas[ID_Unsafe].SID;
                else
                    SID = null;
            }
        }

        /// <summary>
        /// The SID (string ID) of the area.
        /// </summary>
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
            if (string.IsNullOrEmpty(SID) && (ID_Unsafe < 0 || AreaData.Areas.Count <= ID_Unsafe))
                throw new Exception($"SaveData contains invalid AreaStats with no SID and out-of-range ID of {ID_Unsafe} / {AreaData.Areas.Count}");

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

    [Obsolete("Use AreaStats members instead.")]
    public static class AreaStatsExt {

        /// <summary>
        /// Get an AreaKey for this area.
        /// </summary>
        public static AreaKey ToKey(this patch_AreaStats self, AreaMode mode)
            => new AreaKey(self.ID, mode).SetSID(self.SID);

        /// <summary>
        /// Get the name of the level set this area belongs to.
        /// </summary>
        [Obsolete("Use AreaStats.LevelSet instead.")]
        public static string GetLevelSet(this AreaStats self)
            => ((patch_AreaStats) self).LevelSet;

        /// <summary>
        /// Get the SID (string ID) of the area.
        /// </summary>
        [Obsolete("Use AreaStats.SID instead.")]
        public static string GetSID(this AreaStats self)
            => ((patch_AreaStats) self).SID;
        /// <summary>
        /// Set the SID (string ID) of the area.
        /// </summary>
        [Obsolete("Use AreaStats.SID instead.")]
        public static AreaStats SetSID(this AreaStats self, string value) {
            ((patch_AreaStats) self).SID = value;
            return self;
        }

    }
}
