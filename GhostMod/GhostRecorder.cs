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
    public class GhostRecorder : Entity {

        public Player Player;

        public GhostData Data;

        public GhostRecorder(Player player)
            : base() {
            Player = player;
            Tag = Tags.HUD;
        }

        public override void Update() {
            base.Update();

            if ((GhostModule.Settings.Mode & GhostModuleMode.Record) != GhostModuleMode.Record ||
                Player == null)
                Data = null;

            RecordData();
        }

        public void RecordData() {
            if (Data == null)
                return;

            // A data frame is always a new frame, no matter if the previous one lacks data or not.
            Data.Frames.Add(new GhostFrame {
                HasData = true,

                InControl = Player.InControl,

                Position = Player.Position,
                Speed = Player.Speed,
                Rotation = Player.Sprite.Rotation,
                Scale = Player.Sprite.Scale,
                Color = Player.Sprite.Color,

                Facing = Player.Facing,

                CurrentAnimationID = Player.Sprite.CurrentAnimationID,
                CurrentAnimationFrame = Player.Sprite.CurrentAnimationFrame,

                HairColor = Player.Hair.Color,
                HairSimulateMotion = Player.Hair.SimulateMotion
            });
        }

    }
}
