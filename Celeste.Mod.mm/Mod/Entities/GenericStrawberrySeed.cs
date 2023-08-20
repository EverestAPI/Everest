using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Mod.Entities {
    [Tracked(false)]
    public class GenericStrawberrySeed : Entity {

        private const float LoseDelay = 0.25f;
        private const float LoseGraceTime = 0.15f;

        public static ParticleType P_Burst;

        public bool Collected {
            get {
                return follower.HasLeader || finished;
            }
        }
        
        private int index;
        private Vector2 start;
        
        private float canLoseTimer;
        private float loseTimer;
        private bool finished;
        private bool losing;
        
        private Sprite sprite;
        private bool ghost;

        public IStrawberrySeeded Strawberry;

        private Player player;
        private Follower follower;
        private Platform attached;
        private Level level;

        private Wiggler wiggler;
        private SineWave sine;
        private Tween lightTween;
        private VertexLight light;
        private BloomPoint bloom;
        private Shaker shaker;

        public GenericStrawberrySeed(IStrawberrySeeded strawberry, Vector2 position, int index, bool ghost) 
            : base(position) {
            Strawberry = strawberry;
            Depth = Depths.Pickups;
            start = Position;
            Collider = new Hitbox(12f, 12f, -6f, -6f);
            this.index = index;
            this.ghost = ghost;

            Add(follower = new Follower(OnGainLeader, OnLoseLeader));
            follower.FollowDelay = 0.2f;
            follower.PersistentFollow = false;

            Add(new StaticMover {
                SolidChecker = solid => solid.CollideCheck(this),
                OnAttach = platform => {
                    Depth = Depths.Top;
                    Collider = new Hitbox(24f, 24f, -12f, -12f);
                    attached = platform;
                    start = Position - platform.Position;
                }
            });
            Add(new PlayerCollider(OnPlayer));

            Add(wiggler = Wiggler.Create(0.5f, 4f, v => {
                sprite.Scale = Vector2.One * (1f + 0.2f * v);
            }, false, false));
            Add(sine = new SineWave(0.5f, 0f).Randomize());
            Add(shaker = new Shaker(false, null));
            Add(bloom = new BloomPoint(1f, 12f));
            Add(light = new VertexLight(Color.White, 1f, 16, 24));
            Add(lightTween = light.CreatePulseTween());

            if (P_Burst == null)
                P_Burst = StrawberrySeed.P_Burst;
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            level = scene as Level;

            sprite = GFX.SpriteBank.Create(ghost ? "ghostberrySeed" : ((level.Session.Area.Mode == AreaMode.CSide) ? "goldberrySeed" : "strawberrySeed"));
            sprite.Position = new Vector2(sine.Value * 2f, sine.ValueOverTwo * 1f);
            Add(sprite);
            if (ghost)
                sprite.Color = Color.White * 0.8f;

            int seedCount = Scene.Tracker.CountEntities<GenericStrawberrySeed>();
            float offset = 1f - index / (seedCount + 1f);
            offset = 0.25f + offset * 0.75f;
            sprite.PlayOffset("idle", offset, false);
            sprite.OnFrameChange = s => {
                if (Visible && sprite.CurrentAnimationID == "idle" && sprite.CurrentAnimationFrame == 19) {
                    Audio.Play(SFX.game_gen_seed_pulse, Position, "count", index);
                    lightTween.Start();
                    level.Displacement.AddBurst(Position, 0.6f, 8f, 20f, 0.2f, null, null);
                }
            };

            P_Burst.Color = sprite.Color;
        }

        public override void Update() {
            base.Update();

            if (!finished) {
                if (canLoseTimer > 0f)
                    canLoseTimer -= Engine.DeltaTime;
                else
                if (follower.HasLeader && player.LoseShards)
                    losing = true;

                if (losing) {
                    if (loseTimer <= 0f || player.Speed.Y < 0f) {
                        player.Leader.LoseFollower(follower);
                        losing = false;
                    } else
                    if (player.LoseShards)
                        loseTimer -= Engine.DeltaTime;
                    else {
                        loseTimer = LoseGraceTime;
                        losing = false;
                    }
                }

                sprite.Position = new Vector2(sine.Value * 2f, sine.ValueOverTwo * 1f) + shaker.Value;
            } else
                light.Alpha = Calc.Approach(light.Alpha, 0f, Engine.DeltaTime * 4f);
        }

        private void OnPlayer(Player player) {
            Audio.Play(SFX.game_gen_seed_touch, Position, "count", index);
            this.player = player;
            player.Leader.GainFollower(follower);

            Collidable = false;
            Depth = Depths.Top;

            bool haveAllSeeds = true;
            foreach (GenericStrawberrySeed strawberrySeed in Strawberry.Seeds)
                if (!strawberrySeed.follower.HasLeader) {
                    haveAllSeeds = false;
                    break;
                }

            if (haveAllSeeds)
                Scene.Add(new CSGEN_GenericStrawberrySeeds(Strawberry));
        }

        private void OnGainLeader() {
            wiggler.Start();
            canLoseTimer = LoseDelay;
            loseTimer = LoseGraceTime;
        }

        private void OnLoseLeader() {
            if (!finished)
                Add(new Coroutine(ReturnRoutine(), true));
        }

        private IEnumerator ReturnRoutine() {
            Audio.Play(SFX.game_gen_seed_poof, Position);
            Collidable = false;
            sprite.Scale = Vector2.One * 2f;
            yield return 0.05f;


            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);

            for (int i = 0; i < 6; i++) {
                float dir = Calc.Random.NextFloat((float) Math.PI * 2);
                level.ParticlesFG.Emit(P_Burst, 1, Position + Calc.AngleToVector(dir, 4f), Vector2.Zero, dir);
            }

            Visible = false;
            yield return 0.3f + index * 0.1f;


            Audio.Play(SFX.game_gen_seed_reappear, Position, "count", index);
            Position = start;
            if (attached != null)
                Position += attached.Position;

            shaker.ShakeFor(0.4f, false);
            sprite.Scale = Vector2.One;
            Visible = true;
            Collidable = true;
            level.Displacement.AddBurst(Position, 0.2f, 8f, 28f, 0.2f);
            yield break;
        }

        public void OnAllCollected() {
            finished = true;
            follower.Leader?.LoseFollower(follower);
            Depth = -2000002;
            Tag = Tags.FrozenUpdate;
            wiggler.Start();
        }

        public void StartSpinAnimation(Vector2 averagePos, Vector2 centerPos, float angleOffset, float time) {
            float spinLerp = 0f;
            Vector2 start = Position;
            sprite.Play("noFlash", false, false);

            Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeIn, time / 2f, true);
            tween.OnUpdate = t => {
                spinLerp = t.Eased;
            };
            Add(tween);

            tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, time, true);
            tween.OnUpdate = t => {
                float angleRadians = (float) Math.PI / 2f + angleOffset - MathHelper.Lerp(0f, 32.201324f, t.Eased);
                Vector2 value = Vector2.Lerp(averagePos, centerPos, spinLerp);
                Vector2 value2 = value + Calc.AngleToVector(angleRadians, 25f);
                Position = Vector2.Lerp(start, value2, spinLerp);
            };
            Add(tween);
        }

        public void StartCombineAnimation(Vector2 centerPos, float time, ParticleSystem particleSystem) {
            Vector2 position = Position;
            float startAngle = Calc.Angle(centerPos, position);

            Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.BigBackIn, time, true);
            tween.OnUpdate = t => {
                float angleRadians = MathHelper.Lerp(startAngle, startAngle - (float) Math.PI * 2, Ease.CubeIn(t.Percent));
                float length = MathHelper.Lerp(25f, 0f, t.Eased);
                Position = centerPos + Calc.AngleToVector(angleRadians, length);
            };
            tween.OnComplete = t => {
                Visible = false;
                for (int i = 0; i < 6; i++) {
                    float dir = Calc.Random.NextFloat((float) Math.PI * 2);
                    particleSystem.Emit(P_Burst, 1, Position + Calc.AngleToVector(dir, 4f), Vector2.Zero, dir);
                }
                RemoveSelf();
            };
            Add(tween);
        }

    }
}
