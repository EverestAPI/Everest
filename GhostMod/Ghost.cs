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

        public PlayerSprite Sprite;
        public PlayerHair Hair;
        public int MachineState;

        public GhostData Data;
        public int FrameIndex = 0;
        public GhostFrame Frame => !GhostModule.Settings.Enabled || Data == null ? default(GhostFrame) : Data[FrameIndex];

        public Ghost(Player player)
            : base(player.Position) {
            Player = player;

            Depth = -1;

            Sprite = new PlayerSprite(player.Sprite.Mode);
            Sprite.HairCount = player.Sprite.HairCount;
            Add(Hair = new PlayerHair(Sprite));
            Add(Sprite);

            Hair.Color = Player.NormalHairColor;
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            Hair.Facing = Frame.Facing;
            Hair.Start();
            UpdateHair();
        }

        public void UpdateHair() {
            if (!Frame.HasData)
                return;

            Hair.Color = Frame.HairColor;
            Hair.Facing = Frame.Facing;
            Hair.SimulateMotion = Frame.HairSimulateMotion;
        }

        public void UpdateSprite() {
            if (!Frame.HasData)
                return;

            Position = Frame.Position;
            Sprite.Rotation = Frame.Rotation;
            Sprite.Scale = Frame.Scale;
            Sprite.Color = Frame.Color * GhostModule.Settings.OpacityFactor;

            try {
                if (Sprite.CurrentAnimationID != Frame.CurrentAnimationID)
                    Sprite.Play(Frame.CurrentAnimationID);
                Sprite.SetAnimationFrame(Frame.CurrentAnimationFrame);
            } catch {
                // Play likes to fail randomly as the ID doesn't exist in an underlying dict.
                // Let's ignore this for now.
            }
        }

        public override void Update() {
            Visible = Frame.HasData;

            UpdateSprite();
            UpdateHair();

            base.Update();

            if (!Player.InControl)
                return;

            do {
                FrameIndex++;
            } while (Frame.HasData && !Frame.InControl);
        }

    }
}
