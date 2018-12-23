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
using Microsoft.Xna.Framework;
using System.IO;
using FMOD.Studio;
using Monocle;
using Celeste.Mod.Meta;

namespace Celeste {
    class patch_Cloud : Cloud {

        public bool? Small;

        public patch_Cloud(Vector2 position, bool fragile)
            : base(position, fragile) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchCloudAdded] // ... except for manually manipulating the method via MonoModRules
        public extern new void Added(Scene scene);

        private static bool _IsSmall(bool value, Cloud self)
            => (self as patch_Cloud).IsSmall(value);
        public bool IsSmall(bool value) {
            return Small ?? value;
        }

    }
}
