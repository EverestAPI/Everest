using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities
{
    [Tracked(false)]
    public class GenericStrawberrySeed : Entity
    {
        public bool Collected
        {
            get
            {
                return this.follower.HasLeader || this.finished;
            }
        }

        public GenericStrawberrySeed(IStrawberrySeeded strawberry, Vector2 position, int index, bool ghost) : base(position)
        {
            this.Strawberry = strawberry;
            base.Depth = -100;
            this.start = this.Position;
            base.Collider = new Hitbox(12f, 12f, -6f, -6f);
            this.index = index;
            this.ghost = ghost;
            base.Add(this.follower = new Follower(new Action(this.OnGainLeader), new Action(this.OnLoseLeader)));
            this.follower.FollowDelay = 0.2f;
            this.follower.PersistentFollow = false;
            base.Add(new StaticMover
            {
                SolidChecker = ((Solid s) => s.CollideCheck(this)),
                OnAttach = delegate (Platform p)
                {
                    base.Depth = -1000000;
                    base.Collider = new Hitbox(24f, 24f, -12f, -12f);
                    this.attached = p;
                    this.start = this.Position - p.Position;
                }
            });
            base.Add(new PlayerCollider(new Action<Player>(this.OnPlayer), null, null));
            base.Add(this.wiggler = Wiggler.Create(0.5f, 4f, delegate (float v)
            {
                this.sprite.Scale = Vector2.One * (1f + 0.2f * v);
            }, false, false));
            base.Add(this.sine = new SineWave(0.5f, 0f).Randomize());
            base.Add(this.shaker = new Shaker(false, null));
            base.Add(this.bloom = new BloomPoint(1f, 12f));
            base.Add(this.light = new VertexLight(Color.White, 1f, 16, 24));
            base.Add(this.lightTween = this.light.CreatePulseTween());

            if (P_Burst == null)
                P_Burst = StrawberrySeed.P_Burst;
        }

        public override void Awake(Scene scene)
        {
            this.level = (scene as Level);
            base.Awake(scene);
            this.sprite = GFX.SpriteBank.Create(this.ghost ? "ghostberrySeed" : ((this.level.Session.Area.Mode == AreaMode.CSide) ? "goldberrySeed" : "strawberrySeed"));
            this.sprite.Position = new Vector2(this.sine.Value * 2f, this.sine.ValueOverTwo * 1f);
            base.Add(this.sprite);
            if (this.ghost)
                this.sprite.Color = Color.White * 0.8f;

            int num = base.Scene.Tracker.CountEntities<GenericStrawberrySeed>();
            float num2 = 1f - (float)this.index / ((float)num + 1f);
            num2 = 0.25f + num2 * 0.75f;
            this.sprite.PlayOffset("idle", num2, false);
            this.sprite.OnFrameChange = delegate (string s)
            {
                if (this.Visible && this.sprite.CurrentAnimationID == "idle" && this.sprite.CurrentAnimationFrame == 19)
                {
                    Audio.Play("event:/game/general/seed_pulse", this.Position, "count", (float)this.index);
                    this.lightTween.Start();
                    this.level.Displacement.AddBurst(this.Position, 0.6f, 8f, 20f, 0.2f, null, null);
                }
            };
            GenericStrawberrySeed.P_Burst.Color = this.sprite.Color;
        }

        public override void Update()
        {
            base.Update();

            if (!this.finished)
            {
                if (this.canLoseTimer > 0f)
                    this.canLoseTimer -= Engine.DeltaTime;
                else
                if (this.follower.HasLeader && this.player.LoseShards)
                    this.losing = true;

                if (this.losing)
                {
                    if (this.loseTimer <= 0f || this.player.Speed.Y < 0f)
                    {
                        this.player.Leader.LoseFollower(this.follower);
                        this.losing = false;
                    }
                    else
                    if (this.player.LoseShards)
                        this.loseTimer -= Engine.DeltaTime;
                    else
                    {
                        this.loseTimer = 0.15f;
                        this.losing = false;
                    }
                }

                this.sprite.Position = new Vector2(this.sine.Value * 2f, this.sine.ValueOverTwo * 1f) + this.shaker.Value;
            }
            else
                this.light.Alpha = Calc.Approach(this.light.Alpha, 0f, Engine.DeltaTime * 4f);
        }

        private void OnPlayer(Player player)
        {
            Audio.Play("event:/game/general/seed_touch", this.Position, "count", (float)this.index);
            this.player = player;
            player.Leader.GainFollower(this.follower);
            this.Collidable = false;
            base.Depth = -1000000;
            bool haveAllSeeds = true;
            foreach (GenericStrawberrySeed strawberrySeed in this.Strawberry.Seeds)
                if (!strawberrySeed.follower.HasLeader)
                {
                    haveAllSeeds = false;
                    break;
                }

            if (haveAllSeeds)
                base.Scene.Add(new CSGEN_GenericStrawberrySeeds(this.Strawberry));
        }

        private void OnGainLeader()
        {
            this.wiggler.Start();
            this.canLoseTimer = 0.25f;
            this.loseTimer = 0.15f;
        }

        private void OnLoseLeader()
        {
            if (!this.finished)
                base.Add(new Coroutine(this.ReturnRoutine(), true));
        }

        private IEnumerator ReturnRoutine()
        {
            Audio.Play("event:/game/general/seed_poof", this.Position);
            this.Collidable = false;
            this.sprite.Scale = Vector2.One * 2f;
            yield return 0.05f;
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);

            for (int i = 0; i < 6; i++)
            {
                float dir = Calc.Random.NextFloat(6.2831855f);
                this.level.ParticlesFG.Emit(GenericStrawberrySeed.P_Burst, 1, this.Position + Calc.AngleToVector(dir, 4f), Vector2.Zero, dir);
            }

            this.Visible = false;
            yield return 0.3f + (float)this.index * 0.1f;


            Audio.Play("event:/game/general/seed_reappear", this.Position, "count", (float)this.index);
            this.Position = this.start;
            if (this.attached != null)
                this.Position += this.attached.Position;

            this.shaker.ShakeFor(0.4f, false);
            this.sprite.Scale = Vector2.One;
            this.Visible = true;
            this.Collidable = true;
            this.level.Displacement.AddBurst(this.Position, 0.2f, 8f, 28f, 0.2f, null, null);
            yield break;
        }

        public void OnAllCollected()
        {
            this.finished = true;
            this.follower.Leader.LoseFollower(this.follower);
            base.Depth = -2000002;
            base.Tag = Tags.FrozenUpdate;
            this.wiggler.Start();
        }

        public void StartSpinAnimation(Vector2 averagePos, Vector2 centerPos, float angleOffset, float time)
        {
            float spinLerp = 0f;
            Vector2 start = this.Position;
            this.sprite.Play("noFlash", false, false);
            Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeIn, time / 2f, true);
            tween.OnUpdate = delegate (Tween t)
            {
                spinLerp = t.Eased;
            };
            base.Add(tween);
            tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, time, true);
            tween.OnUpdate = delegate (Tween t)
            {
                float angleRadians = 1.5707964f + angleOffset - MathHelper.Lerp(0f, 32.201324f, t.Eased);
                Vector2 value = Vector2.Lerp(averagePos, centerPos, spinLerp);
                Vector2 value2 = value + Calc.AngleToVector(angleRadians, 25f);
                this.Position = Vector2.Lerp(start, value2, spinLerp);
            };
            base.Add(tween);
        }

        public void StartCombineAnimation(Vector2 centerPos, float time, ParticleSystem particleSystem)
        {
            Vector2 position = this.Position;
            float startAngle = Calc.Angle(centerPos, position);
            Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.BigBackIn, time, true);
            tween.OnUpdate = delegate (Tween t)
            {
                float angleRadians = MathHelper.Lerp(startAngle, startAngle - 6.2831855f, Ease.CubeIn(t.Percent));
                float length = MathHelper.Lerp(25f, 0f, t.Eased);
                this.Position = centerPos + Calc.AngleToVector(angleRadians, length);
            };
            tween.OnComplete = delegate (Tween t)
            {
                this.Visible = false;
                for (int i = 0; i < 6; i++)
                {
                    float num = Calc.Random.NextFloat(6.2831855f);
                    particleSystem.Emit(GenericStrawberrySeed.P_Burst, 1, this.Position + Calc.AngleToVector(num, 4f), Vector2.Zero, num);
                }
                this.RemoveSelf();
            };
            base.Add(tween);
        }

        public static ParticleType P_Burst;
        public IStrawberrySeeded Strawberry;

        private const float LoseDelay = 0.25f;
        private const float LoseGraceTime = 0.15f;
        private Sprite sprite;
        private Follower follower;
        private Wiggler wiggler;
        private Platform attached;
        private SineWave sine;
        private Tween lightTween;
        private VertexLight light;
        private BloomPoint bloom;
        private Shaker shaker;
        private int index;
        private Vector2 start;
        private Player player;
        private Level level;
        private float canLoseTimer;
        private float loseTimer;
        private bool finished;
        private bool losing;
        private bool ghost;
    }
}
