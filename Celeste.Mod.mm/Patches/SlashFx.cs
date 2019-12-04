#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_SlashFx : SlashFx {

        [MonoModLinkFrom("System.Void Celeste.SlashFx::Burst(Microsoft.Xna.Framework.Vector2,System.Single)")]
        public static void _Burst(Vector2 position, float direction) {
            Burst(position, direction);
        }

    }
}
