#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
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

namespace Celeste {
    class patch_TrailManager : TrailManager {

        // Make this signature accessible to older mods.
        public static void Add(Entity entity, Color color, float duration = 1f) {
            TrailManager.Add(entity, color, duration);
        }

    }
}
