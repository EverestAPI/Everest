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
        public GhostFrame? ForcedFrame;
        public GhostFrame Frame => ForcedFrame ?? (Data == null ? default(GhostFrame) : Data[FrameIndex]);
        public bool AutoForward = true;

        public GhostName Name;

        protected float alpha;
        protected float alphaHair;

        public Ghost(Player player)
            : this(player, null) {
        }
        public Ghost(Player player, GhostData data)
            : base(player.Position) {
            Player = player;
            Data = data;

            Depth = 1;

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

            if (Data != null && Data.Name != GhostModule.Settings.Name)
                Scene.Add(Name = new GhostName(this, Data.Name));
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);

            Name?.RemoveSelf();
        }

        public void UpdateHair() {
            if (!Frame.HasData)
                return;

            Hair.Color = Frame.HairColor;
            Hair.Alpha = alphaHair;
            Hair.Facing = Frame.Facing;
            Hair.SimulateMotion = Frame.HairSimulateMotion;
        }

        public void UpdateSprite() {
            if (!Frame.HasData)
                return;

            Position = Frame.Position;
            Sprite.Rotation = Frame.Rotation;
            Sprite.Scale = Frame.Scale;
            Sprite.Color = Frame.Color * alpha;

            Sprite.Rate = Frame.SpriteRate;
            Sprite.Justify = Frame.SpriteJustify;

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
            Visible = GhostModule.Settings.Enabled && Frame.HasData;
            if (Data != null && Data.Dead)
                Visible &= GhostModule.Settings.ShowDeaths;

            if (Data != null && Data.Opacity != null) {
                alpha = Data.Opacity.Value;
                alphaHair = Data.Opacity.Value;
            } else {
                float dist = (Player.Position - Position).LengthSquared();
                dist -= GhostModule.Settings.InnerRadiusDist;
                if (dist < 0f)
                    dist = 0f;
                if (GhostModule.Settings.BorderSize == 0) {
                    dist = dist < GhostModule.Settings.InnerRadiusDist ? 0f : 1f;
                } else {
                    dist /= GhostModule.Settings.BorderSizeDist;
                }
                alpha = Calc.LerpClamp(GhostModule.Settings.InnerOpacityFactor, GhostModule.Settings.OuterOpacityFactor, dist);
                alphaHair = Calc.LerpClamp(GhostModule.Settings.InnerHairOpacityFactor, GhostModule.Settings.OuterHairOpacityFactor, dist);
            }

            Visible &= alpha > 0f;

            if (Name != null)
                Name.Alpha = alpha;

            UpdateSprite();
            UpdateHair();

            base.Update();

            if (!Player.InControl)
                return;

            if (AutoForward && ForcedFrame == null) {
                do {
                    FrameIndex++;
                } while (
                    (Frame.HasData && !Frame.InControl) || // Skip any frames we're not in control in.
                    (!Frame.HasData && FrameIndex < Data.Frames.Count) // Skip any frames not containing the data chunk.
                );
            }
        }

    }
}
