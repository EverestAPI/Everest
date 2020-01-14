using System;
using System.Collections;
using System.Collections.Generic;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities
{
    public class CSGEN_GenericStrawberrySeeds : CutsceneEntity
    {
        public CSGEN_GenericStrawberrySeeds(IStrawberrySeeded strawberry) : base(true, false)
        {
            this.strawberry = strawberry;
        }

        public override void OnBegin(Level level)
        {
            this.cameraStart = level.Camera.Position;
            base.Add(new Coroutine(this.Cutscene(level), true));
        }

        private IEnumerator Cutscene(Level level)
        {
            this.sfx = Audio.Play("event:/game/general/seed_complete_main", this.Position);
            this.snapshot = Audio.CreateSnapshot("snapshot:/music_mains_mute", true);

            Player player = base.Scene.Tracker.GetEntity<Player>();
            bool flag = player != null;
            if (flag)
            {
                this.cameraStart = player.CameraTarget;
            }

            // Shorten code for reading.
            List<GenericStrawberrySeed> seeds = strawberry.Seeds;
            Entity entity = (Entity) this.strawberry;

            foreach (GenericStrawberrySeed seed in seeds)
                seed.OnAllCollected();

            entity.Depth = -2000002;
            entity.AddTag(Tags.FrozenUpdate);

            yield return 0.35f;


            base.Tag = (Tags.FrozenUpdate | Tags.HUD);
            level.Frozen = true;
            level.FormationBackdrop.Display = true;
            level.FormationBackdrop.Alpha = 0.5f;
            level.Displacement.Clear();
            level.Displacement.Enabled = false;

            Audio.BusPaused("bus:/gameplay_sfx/ambience", new bool?(true));
            Audio.BusPaused("bus:/gameplay_sfx/char", new bool?(true));
            Audio.BusPaused("bus:/gameplay_sfx/game/general/yes_pause", new bool?(true));
            Audio.BusPaused("bus:/gameplay_sfx/game/chapters", new bool?(true));

            yield return 0.1f;


            this.system = new ParticleSystem(-2000002, 50);
            this.system.Tag = Tags.FrozenUpdate;
            level.Add(this.system);
            float angleSep = 6.2831855f / (float)seeds.Count;
            float angle = 1.5707964f;
            Vector2 avg = Vector2.Zero;
            foreach (GenericStrawberrySeed seed in seeds)
                avg += seed.Position;

            avg /= (float)seeds.Count;
            foreach (GenericStrawberrySeed seed in seeds)
            {
                seed.StartSpinAnimation(avg, entity.Position, angle, 4f);
                angle -= angleSep;
            }

            avg = default(Vector2);
            Vector2 target = entity.Position - new Vector2(160f, 90f);
            target = target.Clamp((float)level.Bounds.Left, (float)level.Bounds.Top, (float)(level.Bounds.Right - 320), (float)(level.Bounds.Bottom - 180));
            base.Add(new Coroutine(CutsceneEntity.CameraTo(target, 3.5f, Ease.CubeInOut, 0f), true));
            target = default(Vector2);
            yield return 4f;


            Input.Rumble(RumbleStrength.Light, RumbleLength.Long);
            Audio.Play("event:/game/general/seed_complete_berry", entity.Position);

            foreach (GenericStrawberrySeed seed in seeds)
                seed.StartCombineAnimation(entity.Position, 0.6f, this.system);

            yield return 0.6f;


            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            foreach (GenericStrawberrySeed seed in seeds)
                seed.RemoveSelf();

            this.strawberry.CollectedSeeds();
            yield return 0.5f;


            float dist = (level.Camera.Position - this.cameraStart).Length();
            yield return CutsceneEntity.CameraTo(this.cameraStart, dist / 180f, null, 0f);


            bool flag2 = dist > 80f;
            if (flag2)
            {
                yield return 0.25f;
            }
            level.EndCutscene();
            this.OnEnd(level);
            yield break;
        }

        public override void OnEnd(Level level)
        {
            bool wasSkipped = this.WasSkipped;
            if (wasSkipped)
            {
                Audio.Stop(this.sfx, true);
            }
            level.OnEndOfFrame += delegate ()
            {
                bool wasSkipped2 = this.WasSkipped;
                if (wasSkipped2)
                {
                    foreach (GenericStrawberrySeed strawberrySeed in this.strawberry.Seeds)
                    {
                        strawberrySeed.RemoveSelf();
                    }
                    this.strawberry.CollectedSeeds();
                    level.Camera.Position = this.cameraStart;
                }
                ((Entity)strawberry).Depth = -100;
                ((Entity)strawberry).RemoveTag(Tags.FrozenUpdate);
                level.Frozen = false;
                level.FormationBackdrop.Display = false;
                level.Displacement.Enabled = true;
            };
            base.RemoveSelf();
        }

        private void EndSfx()
        {
            Audio.BusPaused("bus:/gameplay_sfx/ambience", new bool?(false));
            Audio.BusPaused("bus:/gameplay_sfx/char", new bool?(false));
            Audio.BusPaused("bus:/gameplay_sfx/game/general/yes_pause", new bool?(false));
            Audio.BusPaused("bus:/gameplay_sfx/game/chapters", new bool?(false));
            Audio.ReleaseSnapshot(this.snapshot);
        }

        public override void Removed(Scene scene)
        {
            this.EndSfx();
            base.Removed(scene);
        }

        public override void SceneEnd(Scene scene)
        {
            this.EndSfx();
            base.SceneEnd(scene);
        }

        private IStrawberrySeeded strawberry;

        private Vector2 cameraStart;

        private ParticleSystem system;

        private EventInstance snapshot;

        private EventInstance sfx;
    }
}
