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
using Microsoft.Xna.Framework;
using System.Collections;
using Celeste.Mod.Meta;

namespace Celeste {
    class patch_HeartGem : HeartGem {

        public patch_HeartGem(Vector2 position)
            : base(position) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchHeartGemCollectRoutine] // ... except for manually manipulating the method via MonoModRules
        private extern IEnumerator CollectRoutine(Player player);

        private bool IsCompleteArea(bool value) {
            MapMetaModeProperties meta = (Scene as Level)?.Session.MapData.GetMeta();
            if (meta?.HeartIsEnd != null)
                return meta.HeartIsEnd.Value;

            return value;
        }

    }
}
