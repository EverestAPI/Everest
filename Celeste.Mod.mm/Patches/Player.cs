#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Runtime.CompilerServices;

namespace Celeste {
    class patch_Player : Player {

        // We're effectively in Player, but still need to "expose" private fields to our mod.
        private bool wasDashB;

        private static int diedInGBJ = 0;
        private int framesAlive;
        private Level level;
        
        public new int MaxDashes {
            get {
                if (SaveData.Instance.Assists.DashMode != Assists.DashModes.Normal && level?.InCutscene == false) {
                    return 2;
                }

                return Inventory.Dashes;
            }
        }


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

            framesAlive = int.MaxValue;

            Level level = Scene as Level;
            if (level != null) {
                framesAlive = 0;
            }

            Everest.Events.Player.Spawn(this);
        }

        [MonoModReplace]
        private void CreateTrail() {
            TrailManager.Add(this, GetCurrentTrailColor(), 1f);
        }

        public extern void orig_Update();
        public override void Update() {
            orig_Update();

            Level level = Scene as Level;
            if (level == null)
                return;
            if (level.CanPause && framesAlive < int.MaxValue)
                framesAlive++;
            if (framesAlive >= 8)
                diedInGBJ = 0;
        }

        public extern PlayerDeadBody orig_Die(Vector2 direction, bool evenIfInvincible, bool registerDeathInStats);
        public new PlayerDeadBody Die(Vector2 direction, bool evenIfInvincible = false, bool registerDeathInStats = true) {
            Level level = Scene as Level;

            // 2 catches spawn-blade-kill GBJs.
            // 4 catches spawn-OOB-kill GBJs.
            if (framesAlive < 6 && level != null) {
                diedInGBJ++;
                if (diedInGBJ != 0 && (diedInGBJ % 2) == 0 && level.Session.Area.GetLevelSet() != "Celeste" && !CoreModule.Settings.DisableAntiSoftlock) {
                    level.Pause();
                    return null;
                }
            }

            PlayerDeadBody orig = orig_Die(direction, evenIfInvincible, registerDeathInStats);
            Everest.Events.Player.Die(this);
            return orig;
        }

        private extern void orig_BoostBegin();
        private void BoostBegin() {
            if (SceneAs<Level>()?.Session.MapData.GetMeta()?.TheoInBubble ?? false) {
                RefillDash();
                RefillStamina();
            }
            else {
                orig_BoostBegin();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Color GetCurrentTrailColor() => GetTrailColor(wasDashB);
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Color GetTrailColor(bool wasDashB) {
            return wasDashB ? NormalHairColor : UsedHairColor;
        }

        public Vector2 ExplodeLaunch(Vector2 from, bool snapUp = true) {
            return ExplodeLaunch(from, snapUp, false);
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
