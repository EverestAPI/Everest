using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.Entities {
    public class CSGEN_GenericStrawberrySeeds : CutsceneEntity {

        private IStrawberrySeeded strawberry;

        private Vector2 cameraStart;

        private ParticleSystem system;
        private EventInstance snapshot;
        private EventInstance sfx;

        public CSGEN_GenericStrawberrySeeds(IStrawberrySeeded strawberry) 
            : base(true, false) {
            this.strawberry = strawberry;
        }

        public override void OnBegin(Level level) {
            cameraStart = level.Camera.Position;
            Add(new Coroutine(Cutscene(level)));
        }

        private IEnumerator Cutscene(Level level) {
            sfx = Audio.Play(SFX.game_gen_seed_complete_main, Position);
            snapshot = Audio.CreateSnapshot(Snapshots.MAIN_DOWN);

            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null)
                cameraStart = player.CameraTarget;

            // Shorten code for reading.
            List<GenericStrawberrySeed> seeds = strawberry.Seeds;
            Entity entity = (Entity) strawberry;

            foreach (GenericStrawberrySeed seed in seeds)
                seed.OnAllCollected();

            entity.Depth = -2000002;
            entity.AddTag(Tags.FrozenUpdate);

            yield return 0.35f;


            Tag = Tags.FrozenUpdate | Tags.HUD;
            level.Frozen = true;
            level.FormationBackdrop.Display = true;
            level.FormationBackdrop.Alpha = 0.5f;
            level.Displacement.Clear();
            level.Displacement.Enabled = false;

            Audio.BusPaused(Buses.AMBIENCE, true);
            Audio.BusPaused(Buses.CHAR, true);
            Audio.BusPaused(Buses.YES_PAUSE, true);
            Audio.BusPaused(Buses.CHAPTERS, true);

            yield return 0.1f;


            system = new ParticleSystem(-2000002, 50);
            system.Tag = Tags.FrozenUpdate;
            level.Add(system);
            float angleSep = (float) Math.PI * 2 / seeds.Count;
            float angle = (float) Math.PI / 2;
            Vector2 avg = Vector2.Zero;
            foreach (GenericStrawberrySeed seed in seeds)
                avg += seed.Position;

            avg /= seeds.Count;
            foreach (GenericStrawberrySeed seed in seeds) {
                seed.StartSpinAnimation(avg, entity.Position, angle, 4f);
                angle -= angleSep;
            }

            avg = default;
            Vector2 target = entity.Position - new Vector2(160f, 90f);
            target = target.Clamp(level.Bounds.Left, level.Bounds.Top, level.Bounds.Right - 320, level.Bounds.Bottom - 180);
            Add(new Coroutine(CameraTo(target, 3.5f, Ease.CubeInOut)));
            target = default;
            yield return 4f;


            Input.Rumble(RumbleStrength.Light, RumbleLength.Long);
            Audio.Play(SFX.game_gen_seed_complete_berry, entity.Position);

            foreach (GenericStrawberrySeed seed in seeds)
                seed.StartCombineAnimation(entity.Position, 0.6f, system);

            yield return 0.6f;


            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            foreach (GenericStrawberrySeed seed in seeds)
                seed.RemoveSelf();

            strawberry.CollectedSeeds();
            yield return 0.5f;


            float dist = (level.Camera.Position - cameraStart).Length();
            yield return CameraTo(cameraStart, dist / 180f);


            if (dist > 80f)
                yield return 0.25f;

            level.EndCutscene();
            OnEnd(level);
            yield break;
        }

        public override void OnEnd(Level level) {
            if (WasSkipped)
                Audio.Stop(sfx, true);

            level.OnEndOfFrame += () => {
                if (WasSkipped) {
                    foreach (GenericStrawberrySeed strawberrySeed in strawberry.Seeds)
                        strawberrySeed.RemoveSelf();

                    strawberry.CollectedSeeds();
                    level.Camera.Position = cameraStart;
                }
                ((Entity) strawberry).Depth = Depths.Pickups;
                ((Entity) strawberry).RemoveTag(Tags.FrozenUpdate);
                level.Frozen = false;
                level.FormationBackdrop.Display = false;
                level.Displacement.Enabled = true;
            };

            RemoveSelf();
        }

        private void EndSfx() {
            Audio.BusPaused(Buses.AMBIENCE, false);
            Audio.BusPaused(Buses.CHAR, false);
            Audio.BusPaused(Buses.YES_PAUSE, false);
            Audio.BusPaused(Buses.CHAPTERS, false);
            Audio.ReleaseSnapshot(snapshot);
        }

        public override void Removed(Scene scene) {
            EndSfx();
            base.Removed(scene);
        }

        public override void SceneEnd(Scene scene) {
            EndSfx();
            base.SceneEnd(scene);
        }

    }
}
