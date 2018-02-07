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

        // The below code works... but causes issues when convering ldarg.1, ldfld* to ldarg.1, call.
        // Most other cases already pass the address as "this" to the ldfld and
        // all general purpose conversions aren't general purpose enough.
        // We're f'd until we find a solution.
        // -ade

        /*
        // If ID_Unsafe becomes a property, use this.
        // [MonoModHook("System.Int32 Celeste.AreaKey::get_ID_Unsafe()")]
        // [MonoModHook("System.Void Celeste.AreaKey::set_ID_Unsafe(System.Int32)")]
        [MonoModHook("System.Int32 Celeste.AreaKey::ID_Unsafe")]
        public int ID;

        // ID hooks _Unsafe, meaning any access to this field affects ID instead.
        [MonoModRemove] // We don't want to preserve the field.
        public int ID_Unsafe;

        // Legacy code sets ID, but we perform SID-based lookups behind the scenes.
        // Tell MonoMod to relink all direct (non-serialization-related) ID modifications
        // and apply them on ID_Safe instead.
        // Yes, relinking fields to properties works because MonoMod handles that for us.
        [XmlIgnore]
        [MonoModHook("System.Int32 Celeste.AreaKey::ID")]
        public int ID_Safe {
            get {
                return ID_Unsafe;
            }
            set {
                ID_Unsafe = value;
                if (value == -1)
                    return;
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
                if (string.IsNullOrEmpty(value) && ID_Unsafe != -1)
                    // We don't actually check if we're in bounds as we want an exception.
                    value = AreaData.Areas[ID_Unsafe].GetSID();
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
                ID_Unsafe = AreaDataExt.Get(value)?.ID ?? ID_Unsafe;
            }
        }
        */

        [MonoModHook("System.Int32 Celeste.AreaKey::ID_Unsafe")]
        public int ID;

        // ID hooks _Unsafe, meaning any access to this field affects ID instead.
        [MonoModRemove] // We don't want to preserve the field.
        public int ID_Unsafe;

        [XmlAttribute]
        public AreaMode Mode;

        public const int SIDLength = 511;
        [XmlIgnore]
        public fixed char _SID[SIDLength + 1];
        [XmlIgnore]
        public int SIDID;
        [XmlAttribute]
        public string SID {
            get {
                string value;
                fixed (char* ptr = _SID)
                    value = Marshal.PtrToStringUni((IntPtr) ptr);
                if ((SIDID != ID || string.IsNullOrEmpty(value)) && ID_Unsafe != -1)
                    // We don't actually check if we're in bounds as we want an exception.
                    value = AreaData.Areas[ID_Unsafe].GetSID();
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
                ID_Unsafe = AreaDataExt.Get(value)?.ID ?? ID_Unsafe;
                SIDID = ID;
            }
        }



        // Living on the edge...
        [MonoModConstructor]
        [MonoModReplace]
        public patch_AreaKey(int id, AreaMode mode = AreaMode.Normal) {
            Mode = mode;
            ID_Unsafe = id;
            ID = id;
            SIDID = id;
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
                if (ID_Unsafe == -1 || AreaData.Areas[ID_Unsafe].Interlude)
                    return -1;

                string levelSet = LevelSet;
                int index = 0;
                for (int i = 0; i <= ID_Unsafe; i++) {
                    if (AreaData.Areas[i].GetLevelSet() != levelSet)
                        continue;
                    if (AreaData.Areas[i].Interlude)
                        continue;
                    index++;
                }
                return index;
            }
        }

        public extern string orig_ToString();
        public override string ToString() {
            string value = orig_ToString();
            string sid = SID;
            if (sid != null)
                value += " (SID: " + sid + ")";
            return value;
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
