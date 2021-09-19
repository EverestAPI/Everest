#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Celeste {
    class patch_Player : Player {

        // We're effectively in Player, but still need to "expose" private fields to our mod.
        private bool wasDashB;
        private HashSet<Trigger> triggersInside;
        private List<Entity> temp;

        private static int diedInGBJ = 0;
        private int framesAlive;
        private Level level;

        public IntroTypes? OverrideIntroType;

        public new int MaxDashes {
            get {
                if (SaveData.Instance.Assists.DashMode != Assists.DashModes.Normal && level?.InCutscene == false) {
                    return 2;
                }

                return Inventory.Dashes;
            }
        }
        
        public new Vector2 CameraTarget {
            get {
             Vector2 result = default(Vector2);
			Vector2 vector = new Vector2(base.X - (camera.Viewport.Width * camera.Zoom / 2f), base.Y - (camera.Viewport.Height * camera.Zoom / 2f));
			if (StateMachine.State != 18)
			{
				vector += new Vector2(level.CameraOffset.X, level.CameraOffset.Y);
			}
			if (StateMachine.State == 19)
			{
				vector.X += 0.2f * camera.Zoom * Speed.X;
				vector.Y += 0.2f * camera.Zoom * Speed.Y;
			}
			else if (StateMachine.State == 5)
			{
				vector.X += 48 * camera.Zoom * Math.Sign(Speed.X);
				vector.Y += 48 * camera.Zoom * Math.Sign(Speed.Y);
			}
			else if (StateMachine.State == 10)
			{
				vector.Y -= 64f * camera.Zoom;
			}
			else if (StateMachine.State == 18)
			{
				vector.Y += 32f * camera.Zoom;
			}
			if (CameraAnchorLerp.Length() > 0f)
			{
				if (CameraAnchorIgnoreX && !CameraAnchorIgnoreY)
				{
					vector.Y = MathHelper.Lerp(vector.Y, CameraAnchor.Y, CameraAnchorLerp.Y);
				}
				else if (!CameraAnchorIgnoreX && CameraAnchorIgnoreY)
				{
					vector.X = MathHelper.Lerp(vector.X, CameraAnchor.X, CameraAnchorLerp.X);
				}
				else if (CameraAnchorLerp.X == CameraAnchorLerp.Y)
				{
					vector = Vector2.Lerp(vector, CameraAnchor, CameraAnchorLerp.X);
				}
				else
				{
					vector.X = MathHelper.Lerp(vector.X, CameraAnchor.X, CameraAnchorLerp.X);
					vector.Y = MathHelper.Lerp(vector.Y, CameraAnchor.Y, CameraAnchorLerp.Y);
				}
			}
			if (EnforceLevelBounds)
			{
				result.X = MathHelper.Clamp(vector.X, level.Bounds.Left, level.Bounds.Right - (int)(camera.Viewport.Width * camera.Zoom));
				result.Y = MathHelper.Clamp(vector.Y, level.Bounds.Top, level.Bounds.Bottom - (int)(camera.Viewport.Height * camera.Zoom));
			}
			else
			{
				result = vector;
			}
			if (level.CameraLockMode != 0)
			{
				CameraLocker component = base.Scene.Tracker.GetComponent<CameraLocker>();
				if (level.CameraLockMode != Level.CameraLockModes.BoostSequence)
				{
					result.X = Math.Max(result.X, level.Camera.X);
					if (component != null)
					{
						result.X = Math.Min(result.X, Math.Max(level.Bounds.Left, component.Entity.X - component.MaxXOffset));
					}
				}
				if (level.CameraLockMode == Level.CameraLockModes.FinalBoss)
				{
					result.Y = Math.Max(result.Y, level.Camera.Y);
					if (component != null)
					{
						result.Y = Math.Min(result.Y, Math.Max(level.Bounds.Top, component.Entity.Y - component.MaxYOffset));
					}
				}
				else if (level.CameraLockMode == Level.CameraLockModes.BoostSequence)
				{
					level.CameraUpwardMaxY = Math.Min(level.Camera.Y + camera.Viewport.Height * camera.Zoom, level.CameraUpwardMaxY);
					result.Y = Math.Min(result.Y, level.CameraUpwardMaxY);
					if (component != null)
					{
						result.Y = Math.Max(result.Y, Math.Min(level.Bounds.Bottom - (int)(camera.Viewport.Height * camera.Zoom), component.Entity.Y - component.MaxYOffset));
					}
				}
			}
			foreach (Entity entity in base.Scene.Tracker.GetEntities<Killbox>())
			{
				if (entity.Collidable && base.Top < entity.Bottom && base.Right > entity.Left && base.Left < entity.Right)
				{
					result.Y = Math.Min(result.Y, entity.Top - camera.Viewport.Height * camera.Zoom);
				}
			}
			return result;   
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
            if (OverrideIntroType != null) {
                IntroType = OverrideIntroType.Value;
                OverrideIntroType = null;
            }

            orig_Added(scene);

            framesAlive = int.MaxValue;

            if (Scene is Level) {
                framesAlive = 0;
            }

            Everest.Events.Player.Spawn(this);
        }

        [MonoModReplace]
        private void CreateTrail() {
            Vector2 scale = new Vector2(Math.Abs(Sprite.Scale.X) * (float) Facing, Sprite.Scale.Y);
            TrailManager.Add(this, scale, GetCurrentTrailColor());
        }

        [PatchPlayerOrigUpdate] // Manipulate the method via MonoModRules
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

        public bool _IsOverWater() {
            // check if we are 2 pixels over water (or less).
            Rectangle bounds = Collider.Bounds;
            bounds.Height += 2;
            return Scene.CollideCheck<Water>(bounds);
        }

        private extern void orig_UpdateSprite();
        private void UpdateSprite() {
            orig_UpdateSprite();

            // Don't slow down the sprite (f.e. in space) for certain states,
            // as their animations may become unbearably long
            // or desynced from their sounds and thus broken.
            if (StateMachine.State == StIntroWakeUp ||
                StateMachine.State == StStarFly) {
                Sprite.Rate = Sprite.Rate < 0f ? -1f : 1f;
            }
        }

        public extern PlayerDeadBody orig_Die(Vector2 direction, bool evenIfInvincible, bool registerDeathInStats);
        public new PlayerDeadBody Die(Vector2 direction, bool evenIfInvincible = false, bool registerDeathInStats = true) {
            Level level = Scene as Level;
            PlayerDeadBody body = orig_Die(direction, evenIfInvincible, registerDeathInStats);

            if (body != null) {
                // 2 catches spawn-blade-kill GBJs.
                // 4 catches spawn-OOB-kill GBJs.
                if (framesAlive < 6 && level != null) {
                    diedInGBJ++;
                    if (diedInGBJ != 0 && (diedInGBJ % 2) == 0 && level.Session.Area.GetLevelSet() != "Celeste" && !CoreModule.Settings.DisableAntiSoftlock) {
                        level.Pause();
                        return null;
                    }
                }
            }

            Everest.Events.Player.Die(this);
            return body;
        }

        private extern void orig_BoostBegin();
        private void BoostBegin() {
            if (SceneAs<Level>()?.Session.MapData.GetMeta()?.TheoInBubble ?? false) {
                RefillDash();
                RefillStamina();
            } else {
                orig_BoostBegin();
            }
        }

        private extern void orig_WindMove(Vector2 move);
        private void WindMove(Vector2 move) {
            // Don't apply wind on player in the Attract state: this would constantly push the player away from its target.
            // This causes an infinite loop when hitting Badeline bosses.
            if (StateMachine.State != StAttract)
                orig_WindMove(move);
        }

        private extern void orig_WallJump(int dir);
        private void WallJump(int dir) {
            if ((Scene as Level).Session.Area.GetLevelSet() != "Celeste") {
                // Fix vertical boost from upwards-moving solids not being applied correctly when dir != -1
                if (LiftSpeed == Vector2.Zero) {
                    Solid solid = CollideFirst<Solid>(Position + Vector2.UnitX * 3f * -dir);
                    if (solid != null) {
                        LiftSpeed = solid.LiftSpeed;
                    }
                }
            }
            orig_WallJump(dir);
        }

        /// <summary>
        /// Get the current player dash trail color.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Color GetCurrentTrailColor() => GetTrailColor(wasDashB);
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Color GetTrailColor(bool wasDashB) {
            if (Sprite.Mode == PlayerSpriteMode.MadelineAsBadeline)
                return wasDashB ? NormalBadelineHairColor : UsedBadelineHairColor;
            return wasDashB ? NormalHairColor : UsedHairColor;
        }

        public Vector2 ExplodeLaunch(Vector2 from, bool snapUp = true) {
            return ExplodeLaunch(from, snapUp, false);
        }

        private extern bool orig_Pickup(Holdable pickup);
        private bool Pickup(Holdable pickup) {
            // Madeline cannot grab something if she is dead...
            // this causes frame-perfect crashes when grabbing a jelly and getting killed at the same time.
            if (Dead) {
                return false;
            }

            return orig_Pickup(pickup);
        }

        public extern void orig_SceneEnd(Scene scene);
        public override void SceneEnd(Scene scene) {
            orig_SceneEnd(scene);

            // if we are not entering PICO-8 or the Reflection Fall cutscene...
            if (!(patch_Engine.NextScene is Pico8.Emulator) && !(patch_Engine.NextScene is OverworldReflectionsFall)) {
                // make sure references to the previous level don't leak if hot reloading inside of a trigger.
                triggersInside?.Clear();
                temp?.Clear();
                level = null;
            }
        }
    }
    public static class PlayerExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <inheritdoc cref="patch_Player.GetCurrentTrailColor"/>
        public static Color GetCurrentTrailColor(this Player self)
            => ((patch_Player) self).GetCurrentTrailColor();

        /// <summary>
        /// Get whether the player is in an intro state or not.
        /// </summary>
        public static bool IsIntroState(this Player self)
            => ((patch_Player) self).IsIntroState;

    }
}
