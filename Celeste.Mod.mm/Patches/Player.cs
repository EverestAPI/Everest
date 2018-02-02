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
    class patch_Player : Player {

        // We're effectively in Player, but still need to "expose" private fields to our mod.
        private bool wasDashB;

        private int trailIndex;

        public patch_Player(Vector2 position, PlayerSpriteMode spriteMode)
            : base(position, spriteMode) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        private void CreateTrail() {
            // TODO: MOVE THIS OUT OF HERE.
            Color color = wasDashB ? NormalHairColor : UsedHairColor;
            if (!Everest.Experiments.RainbowMode || Hair != null)
                color = ((patch_PlayerHair) Hair).GetHairColor(trailIndex, color);
            TrailManager.Add(this, color, 1f);
            trailIndex++;
        }

    }
}
