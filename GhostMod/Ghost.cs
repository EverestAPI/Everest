using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost {
    public class Ghost : Actor {

        public Player Player;
        public GhostData Data;

        public PlayerSprite Sprite;
        public PlayerHair Hair;
        public int MachineState;

        public int FrameIndex = 0;
        public GhostFrame Frame => !GhostModule.Settings.Enabled || Data == null ? null : Data[FrameIndex];

        public Ghost(Player player, GhostData data)
            : base(player.Position) {
            Player = player;
            Data = data;

            Depth = -1;

            Sprite = new PlayerSprite(player.Sprite.Mode);
            Sprite.HairCount = player.Sprite.HairCount;
            Add(Hair = new PlayerHair(Sprite));
            Add(Sprite);

            Hair.Color = Player.NormalHairColor;
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            if (Frame == null) {
                RemoveSelf();
                return;
            }

            Hair.Facing = Frame.Facing;
            Hair.Start();
            UpdateHair();
        }

        public void UpdateHair() {
            if (Frame == null)
                return;

            Hair.Color = Frame.HairColor;
            Hair.Facing = Frame.Facing;
            Hair.SimulateMotion = Frame.HairSimulateMotion;
        }

        public void UpdateSprite() {
            if (Frame == null)
                return;

            Position = Frame.Position;
            Sprite.Rotation = Frame.Rotation;
            Sprite.Scale = Frame.Scale;
            Sprite.Color = Frame.Color * GhostModule.Settings.OpacityFactor;

            if (Sprite.CurrentAnimationID != Frame.CurrentAnimationID)
                Sprite.Play(Frame.CurrentAnimationID);
            Sprite.SetAnimationFrame(Frame.CurrentAnimationFrame);
        }

        public override void Update() {
            if (Frame == null) {
                RemoveSelf();
                return;
            }

            UpdateSprite();
            UpdateHair();

            base.Update();

            FrameIndex++;
        }

    }
}
