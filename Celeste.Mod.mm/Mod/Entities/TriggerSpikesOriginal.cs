using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Based on TriggerSpikes, to be used by custom maps.
    /// 
    /// TriggerSpikes with the original spike graphics.
    /// </summary>
    [CustomEntity(
        "triggerSpikesOriginalUp = LoadUp",
        "triggerSpikesOriginalDown = LoadDown",
        "triggerSpikesOriginalLeft = LoadLeft",
        "triggerSpikesOriginalRight = LoadRight"
    )]
    public class TriggerSpikesOriginal : Entity {

        public static Entity LoadUp(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
            => new TriggerSpikesOriginal(entityData, offset, Directions.Up);
        public static Entity LoadDown(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
            => new TriggerSpikesOriginal(entityData, offset, Directions.Down);
        public static Entity LoadLeft(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
            => new TriggerSpikesOriginal(entityData, offset, Directions.Left);
        public static Entity LoadRight(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
            => new TriggerSpikesOriginal(entityData, offset, Directions.Right);

        private const float RetractTime = 6f;
        private const float DefaultDelayTime = 0.4f;
        private float delayTime;

        private int size;
        private Directions direction;
        private string overrideType;

        private PlayerCollider pc;

        private Vector2 outwards;

        private Vector2 shakeOffset;

        private string spikeType;

        private SpikeInfo[] spikes;
        private List<MTexture> spikeTextures;

        public TriggerSpikesOriginal(EntityData data, Vector2 offset, Directions dir)
            : this(data.Position + offset, GetSize(data, dir), dir, data.Attr("type", "default"), data.Float("delayTime", DefaultDelayTime)) {
        }

        public TriggerSpikesOriginal(Vector2 position, int size, Directions direction, string overrideType) : this(position, size, direction, overrideType, DefaultDelayTime) { }

        public TriggerSpikesOriginal(Vector2 position, int size, Directions direction, string overrideType, float delayTime)
            : base(position) {
            this.size = size;
            this.direction = direction;
            this.overrideType = overrideType;
            this.delayTime = delayTime;

            switch (direction) {
                case Directions.Up:
                    outwards = new Vector2(0f, -1f);
                    Collider = new Hitbox(size, 3f, 0f, -3f);
                    Add(new SafeGroundBlocker());
                    Add(new LedgeBlocker(UpSafeBlockCheck));
                    break;

                case Directions.Down:
                    outwards = new Vector2(0f, 1f);
                    Collider = new Hitbox(size, 3f, 0f, 0f);
                    break;

                case Directions.Left:
                    outwards = new Vector2(-1f, 0f);
                    Collider = new Hitbox(3f, size, -3f, 0f);

                    Add(new SafeGroundBlocker());
                    Add(new LedgeBlocker(SideSafeBlockCheck));
                    break;

                case Directions.Right:
                    outwards = new Vector2(1f, 0f);
                    Collider = new Hitbox(3f, size, 0f, 0f);

                    Add(new SafeGroundBlocker());
                    Add(new LedgeBlocker(SideSafeBlockCheck));
                    break;
            }

            Add(pc = new PlayerCollider(OnCollide));

            Add(new StaticMover {
                OnShake = OnShake,
                SolidChecker = IsRiding,
                JumpThruChecker = IsRiding
            });

            Depth = -50;
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            AreaData areaData = AreaData.Get(scene);
            spikeType = areaData.Spike;
            if (!string.IsNullOrEmpty(overrideType) && overrideType != "default")
                spikeType = overrideType;

            string dir = direction.ToString().ToLower();

            if (spikeType == "tentacles") {
                throw new NotSupportedException("Trigger tentacles currently not supported");
            }

            spikes = new SpikeInfo[size / 8];
            spikeTextures = GFX.Game.GetAtlasSubtextures("danger/spikes/" + spikeType + "_" + dir);
            for (int i = 0; i < spikes.Length; i++) {
                spikes[i].Parent = this;
                spikes[i].Index = i;
                switch (direction) {
                    case Directions.Up:
                        spikes[i].Position = Vector2.UnitX * (i + 0.5f) * 8f + Vector2.UnitY;
                        break;

                    case Directions.Down:
                        spikes[i].Position = Vector2.UnitX * (i + 0.5f) * 8f - Vector2.UnitY;
                        break;

                    case Directions.Left:
                        spikes[i].Position = Vector2.UnitY * (i + 0.5f) * 8f + Vector2.UnitX;
                        break;

                    case Directions.Right:
                        spikes[i].Position = Vector2.UnitY * (i + 0.5f) * 8f - Vector2.UnitX;
                        break;
                }
            }
        }

        private void OnShake(Vector2 amount) {
            shakeOffset += amount;
        }

        private bool UpSafeBlockCheck(Player player) {
            int dir = 8 * (int) player.Facing;
            int left = (int) ((player.Left + dir - Left) / 4f);
            int right = (int) ((player.Right + dir - Left) / 4f);

            if (right < 0 || left >= spikes.Length)
                return false;

            left = Math.Max(left, 0);
            right = Math.Min(right, spikes.Length - 1);
            for (int i = left; i <= right; i++)
                if (spikes[i].Lerp >= 1f)
                    return true;

            return false;
        }

        private bool SideSafeBlockCheck(Player player) {
            int top = (int) ((player.Top - Top) / 4f);
            int bottom = (int) ((player.Bottom - Top) / 4f);

            if (bottom < 0 || top >= spikes.Length)
                return false;

            top = Math.Max(top, 0);
            bottom = Math.Min(bottom, spikes.Length - 1);
            for (int i = top; i <= bottom; i++)
                if (spikes[i].Lerp >= 1f)
                    return true;

            return false;
        }

        private void OnCollide(Player player) {
            int minIndex;
            int maxIndex;
            GetPlayerCollideIndex(player, out minIndex, out maxIndex);
            if (maxIndex < 0 || minIndex >= spikes.Length)
                return;

            minIndex = Math.Max(minIndex, 0);
            maxIndex = Math.Min(maxIndex, spikes.Length - 1);
            for (int i = minIndex; i <= maxIndex; i++)
                if (spikes[i].OnPlayer(player, outwards))
                    break;
        }

        private void GetPlayerCollideIndex(Player player, out int minIndex, out int maxIndex) {
            minIndex = maxIndex = -1;

            switch (direction) {
                case Directions.Up:
                    if (player.Speed.Y >= 0f) {
                        minIndex = (int) ((player.Left - Left) / 8f);
                        maxIndex = (int) ((player.Right - Left) / 8f);
                    }
                    break;

                case Directions.Down:
                    if (player.Speed.Y <= 0f) {
                        minIndex = (int) ((player.Left - Left) / 8f);
                        maxIndex = (int) ((player.Right - Left) / 8f);
                    }
                    break;

                case Directions.Left:
                    if (player.Speed.X >= 0f) {
                        minIndex = (int) ((player.Top - Top) / 8f);
                        maxIndex = (int) ((player.Bottom - Top) / 8f);
                    }
                    break;

                case Directions.Right:
                    if (player.Speed.X <= 0f) {
                        minIndex = (int) ((player.Top - Top) / 8f);
                        maxIndex = (int) ((player.Bottom - Top) / 8f);
                    }
                    break;
            }
        }

        private bool PlayerCheck(int spikeIndex) {
            Player player = CollideFirst<Player>();
            if (player == null)
                return false;

            int minIndex;
            int maxIndex;
            GetPlayerCollideIndex(player, out minIndex, out maxIndex);
            return minIndex <= spikeIndex + 1 && maxIndex >= spikeIndex - 1;
        }

        private static int GetSize(EntityData data, Directions dir) {
            return
                dir > Directions.Down ?
                data.Height :
                data.Width;
        }

        public override void Update() {
            base.Update();
            for (int i = 0; i < spikes.Length; i++)
                spikes[i].Update();
        }

        public override void Render() {
            base.Render();

            Vector2 justify = Vector2.One * 0.5f;
            switch (direction) {
                case Directions.Up:
                    justify = new Vector2(0.5f, 1f);
                    break;
                case Directions.Down:
                    justify = new Vector2(0.5f, 0f);
                    break;
                case Directions.Left:
                    justify = new Vector2(1f, 0.5f);
                    break;
                case Directions.Right:
                    justify = new Vector2(0f, 0.5f);
                    break;
            }

            for (int i = 0; i < spikes.Length; i++) {
                MTexture tex = spikeTextures[spikes[i].TextureIndex];
                Vector2 pos = Position + shakeOffset + spikes[i].Position + outwards * (-4f + spikes[i].Lerp * 4f);
                tex.DrawJustified(pos, justify);
            }
        }

        private bool IsRiding(Solid solid) {
            switch (direction) {
                case Directions.Up:
                    return CollideCheckOutside(solid, Position + Vector2.UnitY);
                case Directions.Down:
                    return CollideCheckOutside(solid, Position - Vector2.UnitY);
                case Directions.Left:
                    return CollideCheckOutside(solid, Position + Vector2.UnitX);
                case Directions.Right:
                    return CollideCheckOutside(solid, Position - Vector2.UnitX);
                default:
                    return false;
            }
        }

        private bool IsRiding(JumpThru jumpThru) {
            Directions directions = direction;
            return directions == Directions.Up && CollideCheck(jumpThru, Position + Vector2.UnitY);
        }

        public enum Directions {
            Up,
            Down,
            Left,
            Right
        }

        private struct SpikeInfo {
            public TriggerSpikesOriginal Parent;
            public int Index;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
            public int TextureIndex;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

            public Vector2 Position;

            public bool Triggered;

            public float RetractTimer;
            public float DelayTimer;
            public float Lerp;

            public void Update() {
                if (Triggered) {
                    if (DelayTimer > 0f) {
                        DelayTimer -= Engine.DeltaTime;
                        if (DelayTimer <= 0f) {
                            if (PlayerCheck()) {
                                DelayTimer = 0.05f;
                            } else {
                                Audio.Play("event:/game/03_resort/fluff_tendril_emerge", Parent.Position + Position);
                            }
                        }
                    } else {
                        Lerp = Calc.Approach(Lerp, 1f, 8f * Engine.DeltaTime);
                    }

                } else {
                    Lerp = Calc.Approach(Lerp, 0f, 4f * Engine.DeltaTime);
                    if (Lerp <= 0f) {
                        Triggered = false;
                    }
                }
            }

            public bool PlayerCheck() {
                return Parent.PlayerCheck(Index);
            }

            public bool OnPlayer(Player player, Vector2 outwards) {
                if (!Triggered) {
                    Audio.Play("event:/game/03_resort/fluff_tendril_touch", Parent.Position + Position);
                    Triggered = true;
                    DelayTimer = Parent.delayTime;
                    RetractTimer = RetractTime;
                    return false;
                }

                if (Lerp >= 1f) {
                    player.Die(outwards);
                    return true;
                }

                return false;
            }
        }

    }
}
