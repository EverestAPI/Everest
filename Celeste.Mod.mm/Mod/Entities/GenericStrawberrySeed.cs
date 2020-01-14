using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities
{
    // Token: 0x0200032C RID: 812
    [Tracked(false)]
    public class GenericStrawberrySeed : Entity
    {
        // Token: 0x170001D5 RID: 469
        // (get) Token: 0x060019A5 RID: 6565 RVA: 0x000B849C File Offset: 0x000B669C
        public bool Collected
        {
            get
            {
                return this.follower.HasLeader || this.finished;
            }
        }

        // Token: 0x060019A6 RID: 6566 RVA: 0x000B84C4 File Offset: 0x000B66C4
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

        // Token: 0x060019A7 RID: 6567 RVA: 0x000B868C File Offset: 0x000B688C
        public override void Awake(Scene scene)
        {
            this.level = (scene as Level);
            base.Awake(scene);
            this.sprite = GFX.SpriteBank.Create(this.ghost ? "ghostberrySeed" : ((this.level.Session.Area.Mode == AreaMode.CSide) ? "goldberrySeed" : "strawberrySeed"));
            this.sprite.Position = new Vector2(this.sine.Value * 2f, this.sine.ValueOverTwo * 1f);
            base.Add(this.sprite);
            bool flag = this.ghost;
            if (flag)
            {
                this.sprite.Color = Color.White * 0.8f;
            }
            int num = base.Scene.Tracker.CountEntities<GenericStrawberrySeed>();
            float num2 = 1f - (float)this.index / ((float)num + 1f);
            num2 = 0.25f + num2 * 0.75f;
            this.sprite.PlayOffset("idle", num2, false);
            this.sprite.OnFrameChange = delegate (string s)
            {
                bool flag2 = this.Visible && this.sprite.CurrentAnimationID == "idle" && this.sprite.CurrentAnimationFrame == 19;
                if (flag2)
                {
                    Audio.Play("event:/game/general/seed_pulse", this.Position, "count", (float)this.index);
                    this.lightTween.Start();
                    this.level.Displacement.AddBurst(this.Position, 0.6f, 8f, 20f, 0.2f, null, null);
                }
            };
            GenericStrawberrySeed.P_Burst.Color = this.sprite.Color;
        }

        // Token: 0x060019A8 RID: 6568 RVA: 0x000B87CC File Offset: 0x000B69CC
        public override void Update()
        {
            base.Update();
            bool flag = !this.finished;
            if (flag)
            {
                bool flag2 = this.canLoseTimer > 0f;
                if (flag2)
                {
                    this.canLoseTimer -= Engine.DeltaTime;
                }
                else
                {
                    bool flag3 = this.follower.HasLeader && this.player.LoseShards;
                    if (flag3)
                    {
                        this.losing = true;
                    }
                }
                bool flag4 = this.losing;
                if (flag4)
                {
                    bool flag5 = this.loseTimer <= 0f || this.player.Speed.Y < 0f;
                    if (flag5)
                    {
                        this.player.Leader.LoseFollower(this.follower);
                        this.losing = false;
                    }
                    else
                    {
                        bool loseShards = this.player.LoseShards;
                        if (loseShards)
                        {
                            this.loseTimer -= Engine.DeltaTime;
                        }
                        else
                        {
                            this.loseTimer = 0.15f;
                            this.losing = false;
                        }
                    }
                }
                this.sprite.Position = new Vector2(this.sine.Value * 2f, this.sine.ValueOverTwo * 1f) + this.shaker.Value;
            }
            else
            {
                this.light.Alpha = Calc.Approach(this.light.Alpha, 0f, Engine.DeltaTime * 4f);
            }
        }

        // Token: 0x060019A9 RID: 6569 RVA: 0x000B8948 File Offset: 0x000B6B48
        private void OnPlayer(Player player)
        {
            Audio.Play("event:/game/general/seed_touch", this.Position, "count", (float)this.index);
            this.player = player;
            player.Leader.GainFollower(this.follower);
            this.Collidable = false;
            base.Depth = -1000000;
            bool flag = true;
            foreach (GenericStrawberrySeed strawberrySeed in this.Strawberry.Seeds)
            {
                bool flag2 = !strawberrySeed.follower.HasLeader;
                if (flag2)
                {
                    flag = false;
                }
            }
            bool flag3 = flag;
            if (flag3)
            {
                base.Scene.Add(new CSGEN_GenericStrawberrySeeds(this.Strawberry));
            }
        }

        // Token: 0x060019AA RID: 6570 RVA: 0x000B8A1C File Offset: 0x000B6C1C
        private void OnGainLeader()
        {
            this.wiggler.Start();
            this.canLoseTimer = 0.25f;
            this.loseTimer = 0.15f;
        }

        // Token: 0x060019AB RID: 6571 RVA: 0x000B8A44 File Offset: 0x000B6C44
        private void OnLoseLeader()
        {
            bool flag = !this.finished;
            if (flag)
            {
                base.Add(new Coroutine(this.ReturnRoutine(), true));
            }
        }

        // Token: 0x060019AC RID: 6572 RVA: 0x000B8A72 File Offset: 0x000B6C72
        private IEnumerator ReturnRoutine()
        {
            Audio.Play("event:/game/general/seed_poof", this.Position);
            this.Collidable = false;
            this.sprite.Scale = Vector2.One * 2f;
            yield return 0.05f;
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            int num;
            for (int i = 0; i < 6; i = num + 1)
            {
                float dir = Calc.Random.NextFloat(6.2831855f);
                this.level.ParticlesFG.Emit(GenericStrawberrySeed.P_Burst, 1, this.Position + Calc.AngleToVector(dir, 4f), Vector2.Zero, dir);
                num = i;
            }
            this.Visible = false;
            yield return 0.3f + (float)this.index * 0.1f;
            Audio.Play("event:/game/general/seed_reappear", this.Position, "count", (float)this.index);
            this.Position = this.start;
            bool flag = this.attached != null;
            if (flag)
            {
                this.Position += this.attached.Position;
            }
            this.shaker.ShakeFor(0.4f, false);
            this.sprite.Scale = Vector2.One;
            this.Visible = true;
            this.Collidable = true;
            this.level.Displacement.AddBurst(this.Position, 0.2f, 8f, 28f, 0.2f, null, null);
            yield break;
        }

        // Token: 0x060019AD RID: 6573 RVA: 0x000B8A84 File Offset: 0x000B6C84
        public void OnAllCollected()
        {
            this.finished = true;
            this.follower.Leader.LoseFollower(this.follower);
            base.Depth = -2000002;
            base.Tag = Tags.FrozenUpdate;
            this.wiggler.Start();
        }

        // Token: 0x060019AE RID: 6574 RVA: 0x000B8ADC File Offset: 0x000B6CDC
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

        // Token: 0x060019AF RID: 6575 RVA: 0x000B8B90 File Offset: 0x000B6D90
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

        // Token: 0x04001616 RID: 5654
        public static ParticleType P_Burst;

        // Token: 0x04001617 RID: 5655
        private const float LoseDelay = 0.25f;

        // Token: 0x04001618 RID: 5656
        private const float LoseGraceTime = 0.15f;

        // Token: 0x04001619 RID: 5657
        public IStrawberrySeeded Strawberry;

        // Token: 0x0400161A RID: 5658
        private Sprite sprite;

        // Token: 0x0400161B RID: 5659
        private Follower follower;

        // Token: 0x0400161C RID: 5660
        private Wiggler wiggler;

        // Token: 0x0400161D RID: 5661
        private Platform attached;

        // Token: 0x0400161E RID: 5662
        private SineWave sine;

        // Token: 0x0400161F RID: 5663
        private Tween lightTween;

        // Token: 0x04001620 RID: 5664
        private VertexLight light;

        // Token: 0x04001621 RID: 5665
        private BloomPoint bloom;

        // Token: 0x04001622 RID: 5666
        private Shaker shaker;

        // Token: 0x04001623 RID: 5667
        private int index;

        // Token: 0x04001624 RID: 5668
        private Vector2 start;

        // Token: 0x04001625 RID: 5669
        private Player player;

        // Token: 0x04001626 RID: 5670
        private Level level;

        // Token: 0x04001627 RID: 5671
        private float canLoseTimer;

        // Token: 0x04001628 RID: 5672
        private float loseTimer;

        // Token: 0x04001629 RID: 5673
        private bool finished;

        // Token: 0x0400162A RID: 5674
        private bool losing;

        // Token: 0x0400162B RID: 5675
        private bool ghost;
    }
}
