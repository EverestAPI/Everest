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

        private static int diedInGBJ = 0;
        private int isInGBJ;

        public bool IsIntroState {
            get {
                int state = StateMachine.State;
                return StIntroWalk <= state && state <= StIntroWakeUp;
            }
        }

        public patch_Player(Vector2 position, PlayerSpriteMode spriteMode)
            : base(position, spriteMode) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            orig_Added(scene);

            isInGBJ = int.MaxValue;

            Level level = Scene as Level;
            if (level != null) {
                isInGBJ = 0;
            }

            Everest.Events.Player.Spawn(this);
        }

        public extern void orig_Update();
        public override void Update() {
            orig_Update();

            Level level = Scene as Level;
            if (level == null)
                return;
            if (level.CanPause && isInGBJ < int.MaxValue)
                isInGBJ++;
            if (isInGBJ >= 5)
                diedInGBJ = 0;
        }

        [MonoModReplace]
        private void CreateTrail() {
            TrailManager.Add(this, GetCurrentTrailColor(), 1f);
        }

        public extern PlayerDeadBody orig_Die(Vector2 direction, bool evenIfInvincible, bool registerDeathInStats);

        new public PlayerDeadBody Die(Vector2 direction, bool evenIfInvincible = false, bool registerDeathInStats = true) {
            Level level = Scene as Level;

            if (isInGBJ < 2 && level != null) {
                diedInGBJ++;
                if (diedInGBJ != 0 && (diedInGBJ % 2) == 0 && level.Session.Area.GetSID() != "Celeste") {
                    level.Pause();
                    return null;
                }
            }

            PlayerDeadBody orig = orig_Die(direction, evenIfInvincible, registerDeathInStats);
            Everest.Events.Player.Die(this);
            return orig;
        }

        public Color GetCurrentTrailColor() => GetTrailColor(wasDashB);
        private Color GetTrailColor(bool wasDashB) {
            return wasDashB ? NormalHairColor : UsedHairColor;
        }

    }
    public static class PlayerExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the current player dash trail color.
        /// </summary>
        public static Color GetCurrentTrailColor(this Player self)
            => ((patch_Player) self).GetCurrentTrailColor();

        /// <summary>
        /// Get whether the player is in an intro state or not.
        /// </summary>
        public static bool IsIntroState(this Player self)
            => ((patch_Player) self).IsIntroState;

    }
}
