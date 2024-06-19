#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;
using System;
using System.Xml;
using System.Xml.Serialization;

namespace Celeste {
    // AreaKey is a struct.
    unsafe struct patch_AreaKey {

        public int ID;

        [XmlAttribute]
        public AreaMode Mode;

        public const int SIDLength = 511;
        [XmlIgnore]
        [NonSerialized]
        private string _SID;
        [XmlIgnore]
        [NonSerialized]
        public int SIDID; // Last ID when the SID was set. SID is always set last.
        /// <summary>
        /// The SID (string ID) of the area.
        /// </summary>
        [XmlAttribute]
        public string SID {
            get {
                string value = _SID;
                if ((SIDID != ID || string.IsNullOrEmpty(value)) && 0 <= ID && ID < AreaData.Areas.Count)
                    value = patch_AreaData.Areas[ID].SID;
                return value;
            }
            set {
                _SID = value;
                // We want to force any legacy code to use the SID's ID.
                ID = patch_AreaData.Get(value)?.ID ?? ID;
                SIDID = ID; // Last ID when the SID was set. SID is always set last.
            }
        }

        // Living on the edge...
        [MonoModConstructor]
        [MonoModReplace]
        public patch_AreaKey(int id, AreaMode mode = AreaMode.Normal) {
            Mode = mode;
            ID = id;
            SIDID = id;
            _SID = null;

            // Only set SID if this AreaKey isn't AreaKey.Default or AreaKey.None
            if (id != -1 && AreaData.Areas != null && AreaData.Areas.Count > 0) {
                // We don't actually check if we're in bounds as we want an exception.
                string sid = patch_AreaData.Areas[id].SID;
                // Only set sid after load. During load, sid is still null.
                if (sid != null)
                    SID = sid;
            }
        }

        public string LevelSet {
            get {
                string sid = SID;
                if (string.IsNullOrEmpty(sid))
                    return "Celeste";
                int lastIndexOfSlash = sid.LastIndexOf('/');
                if (lastIndexOfSlash == -1)
                    return "";
                return sid.Substring(0, lastIndexOfSlash);
            }
        }

        public int ChapterIndex {
            [MonoModReplace]
            get {
                if (ID == -1 || AreaData.Areas[ID].Interlude)
                    return -1;

                string levelSet = LevelSet;
                int index = 0;
                for (int i = 0; i <= ID; i++) {
                    if (patch_AreaData.Areas[i].LevelSet != levelSet)
                        continue;
                    if (AreaData.Areas[i].Interlude)
                        continue;
                    index++;
                }
                return index;
            }
        }

        public int RelativeIndex {
            get {
                if (ID == -1)
                    return -1;

                string levelSet = LevelSet;
                int index = 0;
                for (int i = 0; i <= ID; i++) {
                    if (patch_AreaData.Areas[i].LevelSet != levelSet)
                        continue;
                    index++;
                }
                return index;
            }
        }

        public extern string orig_ToString();
        public override string ToString() {
            string value = orig_ToString();
            if (ID < 11)
                return value;
            string sid = SID;
            if (sid != null)
                value += " (SID: " + sid + ")";
            return value;
        }

    }
    public static class AreaKeyExt {

        /// <summary>
        /// Get the name of the level set this area belongs to.
        /// </summary>
        public static string GetLevelSet(this AreaKey self)
            => ((patch_AreaKey) (object) self).LevelSet;

        /// <summary>
        /// Get the SID (string ID) of the area.
        /// </summary>
        public static string GetSID(this AreaKey self)
            => ((patch_AreaKey) (object) self).SID;
        /// <summary>
        /// Set the SID (string ID) of the area.
        /// </summary>
        public static AreaKey SetSID(this AreaKey self, string value) {
            patch_AreaKey p = (patch_AreaKey) (object) self;
            p.SID = value;
            return (AreaKey) (object) p;
        }

        /// <summary>
        /// Get the index of the area relative to the first area in the level set.
        /// Depends on the currently loaded area list.
        /// </summary>
        public static int GetRelativeIndex(this AreaKey self)
            => ((patch_AreaKey) (object) self).RelativeIndex;

    }
}
