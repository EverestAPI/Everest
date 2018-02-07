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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Celeste {
    // AreaKey is a struct.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    unsafe struct patch_AreaKey {

        [XmlAttribute]
        public int ID;

        // Legacy code sets ID, but we perform SID-based lookups behind the scenes.
        // Tell MonoMod to relink all direct (non-serialization-related) ID modifications
        // and apply them on ID_Safe instead.
        // Yes, relinking fields to properties works because MonoMod handles that for us.
        [XmlIgnore]
        [MonoModHook("ID")]
        public int ID_Safe {
            get {
                return ID;
            }
            set {
                ID = value;
                // We don't actually check if we're in bounds as we want an exception.
                string sid = AreaData.Areas[value].GetSID();
                // Only set sid after load. During load, sid is still null.
                if (sid != null)
                    SID = sid;
            }
        }

        [XmlAttribute]
        public AreaMode Mode;

        public const int SIDLength = 511;
        [XmlIgnore]
        public fixed char _SID[SIDLength + 1];
        [XmlAttribute]
        public string SID {
            get {
                string value;
                fixed (char* ptr = _SID)
                    value = Marshal.PtrToStringUni((IntPtr) ptr);
                if (string.IsNullOrEmpty(value) && ID != -1)
                    // We don't actually check if we're in bounds as we want an exception.
                    value = AreaData.Areas[ID].GetSID();
                return value;
            }
            set {
                // Can probably be optimized.
                char[] chars = value.ToCharArray();
                int length = Math.Min(SIDLength - 1, chars.Length);
                fixed (char* to = _SID) {
                    Marshal.Copy(chars, 0, (IntPtr) to, length);
                    for (int i = length; i < SIDLength; i++) {
                        to[i] = '\0';
                    }
                }
                // We want to force any legacy code to use the SID's ID.
                ID = AreaDataExt.Get(value)?.ID ?? ID;
            }
        }

        // Living on the edge...
        [MonoModConstructor]
        [MonoModReplace]
        public patch_AreaKey(int id, AreaMode mode = AreaMode.Normal) {
            ID = id;
            Mode = mode;
            // Only set SID if this AreaKey isn't AreaKey.Default or AreaKey.None
            if (id != -1 && AreaData.Areas != null && AreaData.Areas.Count > 0) {
                // We don't actually check if we're in bounds as we want an exception.
                string sid = AreaData.Areas[id].GetSID();
                // Only set sid after load. During load, sid is still null.
                if (sid != null)
                    SID = sid;
            }
        }

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

        public int ChapterIndex {
            [MonoModReplace]
            get {
                if (AreaDataExt.Get(SID).Interlude)
                    return -1;

                string levelSet = LevelSet;
                int index = 0;
                for (int i = 0; i <= ID; i++) {
                    if (AreaData.Areas[i].GetLevelSet() != levelSet)
                        continue;
                    if (AreaData.Areas[i].Interlude)
                        continue;
                    index++;
                }
                return index;
            }
        }

    }
    public static class AreaKeyExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        unsafe static patch_AreaKey ToPatch(this AreaKey self)
            => *((patch_AreaKey*) &self);

        unsafe static AreaKey ToOrig(this patch_AreaKey self)
            => *((AreaKey*) &self);

        public static string GetLevelSet(this AreaKey self)
            => ((patch_AreaKey) (object) self).LevelSet;

        public static string GetSID(this AreaKey self)
            => ((patch_AreaKey) (object) self).SID;
        public static AreaKey SetSID(this AreaKey self, string value) {
            patch_AreaKey p = self.ToPatch();
            p.SID = value;
            return p.ToOrig();
        }

    }
}
