#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using MonoMod.Utils;
using System;

namespace Monocle {
    class patch_ErrorLog {

        public static extern void orig_Write(Exception e);
        public static void Write(Exception e) {
            Everest.LogDetours();
            e.LogDetailed();
            orig_Write(e);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchErrorLogWrite] // ... except for manually manipulating the method via MonoModRules
        public static extern void Write(string str);

    }
}
