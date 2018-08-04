#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OuiFileSelectSlot : OuiFileSelectSlot {

        public patch_OuiFileSelectSlot(int index, OuiFileSelect fileSelect, SaveData data)
            : base(index, fileSelect, data) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Patching constructors is ugly.
        public extern void orig_ctor(int index, OuiFileSelect fileSelect, SaveData data);
        [MonoModConstructor]
        public void ctor(int index, OuiFileSelect fileSelect, SaveData data) {
            // Temporarily set the current save data to the file slot's save data.
            // This enables filtering the areas by the save data's current levelset.
            SaveData prev = SaveData.Instance;
            SaveData.Instance = data;
            orig_ctor(index, fileSelect, data);
            SaveData.Instance = prev;
        }

    }
}
