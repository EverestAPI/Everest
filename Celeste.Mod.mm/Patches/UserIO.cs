#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
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
    static class patch_UserIO {

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchUnhandledSDL2Platform] // ... except for manually manipulating the method via MonoModRules
        private static extern string GetSavePath();

    }
}
