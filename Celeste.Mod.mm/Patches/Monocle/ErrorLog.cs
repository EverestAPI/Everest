#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using System;

namespace Monocle {
    class patch_ErrorLog {

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchErrorLogWrite] // ... except for manually manipulating the method via MonoModRules
        public static extern void Write(string str);

    }
}
