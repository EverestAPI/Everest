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
    public class GhostRecorder : Component {

        public GhostData Data;

        public GhostRecorder()
            : base(true, false) {
        }

        public override void Added(Entity entity) {
            base.Added(entity);

            if (!GhostModule.Settings.Enabled) {
                RemoveSelf();
                return;
            }
        }

        public override void Update() {
            base.Update();

            Player player = (Player) Entity;

            if (Data == null)
                return;

            if (player.Dead)
                Data.Dead = true;

            Data.Frames.Add(new GhostFrame {
                HasData = true,

                InControl = player.InControl,

                Position = player.Position,
                Speed = player.Speed,
                Rotation = player.Sprite.Rotation,
                Scale = player.Sprite.Scale,
                Color = player.Sprite.Color,

                Facing = player.Facing,

                CurrentAnimationID = player.Sprite.CurrentAnimationID,
                CurrentAnimationFrame = player.Sprite.CurrentAnimationFrame,

                HairColor = player.Hair.Color,
                HairSimulateMotion = player.Hair.SimulateMotion,

                HasInput = true,

                MoveX = Input.MoveX.Value,
                MoveY = Input.MoveY.Value,

                Aim = Input.Aim.Value,
                MountainAim = Input.MountainAim.Value,

                ESC = Input.ESC.Check,
                Pause = Input.Pause.Check,
                MenuLeft = Input.MenuLeft.Check,
                MenuRight = Input.MenuRight.Check,
                MenuUp = Input.MenuUp.Check,
                MenuDown = Input.MenuDown.Check,
                MenuConfirm = Input.MenuConfirm.Check,
                MenuCancel = Input.MenuCancel.Check,
                MenuJournal = Input.MenuJournal.Check,
                QuickRestart = Input.QuickRestart.Check,
                Jump = Input.Jump.Check,
                Dash = Input.Dash.Check,
                Grab = Input.Grab.Check,
                Talk = Input.Talk.Check

            });
        }

    }
}
