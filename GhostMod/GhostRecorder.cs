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

        public override void Added(Scene scene) {
            base.Added(scene);

            if (!GhostModule.Settings.Enabled || Player == null) {
                RemoveSelf();
                return;
            }
        }

        public override void Update() {
            base.Update();

            if (Data == null)
                return;

            RecordData();
        }

        public override void Render() {
            base.Render();

            if (Data == null)
                return;

            RecordInput();
        }

        public void RecordData() {
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

        public void RecordInput() {
            // Check if we've got a data-less input frame. If so, add input to it.
            // If the frame already has got input, add a new input frame.

            bool inputDisabled = MInput.Disabled;
            MInput.Disabled = false;

            GhostFrame frame;
            bool isNew = false;
            if (Data.Frames.Count == 0 || Data[Data.Frames.Count - 1].HasInput) {
                frame = new GhostFrame();
                isNew = true;
            } else {
                frame = Data[Data.Frames.Count - 1];
            }

            frame.HasInput = true;

            frame.MoveX = Input.MoveX.Value;
            frame.MoveY = Input.MoveY.Value;

            frame.Aim = Input.Aim.Value;
            frame.MountainAim = Input.MountainAim.Value;

            frame.ESC = Input.ESC.Check;
            frame.Pause = Input.Pause.Check;
            frame.MenuLeft = Input.MenuLeft.Check;
            frame.MenuRight = Input.MenuRight.Check;
            frame.MenuUp = Input.MenuUp.Check;
            frame.MenuDown = Input.MenuDown.Check;
            frame.MenuConfirm = Input.MenuConfirm.Check;
            frame.MenuCancel = Input.MenuCancel.Check;
            frame.MenuJournal = Input.MenuJournal.Check;
            frame.QuickRestart = Input.QuickRestart.Check;
            frame.Jump = Input.Jump.Check;
            frame.Dash = Input.Dash.Check;
            frame.Grab = Input.Grab.Check;
            frame.Talk = Input.Talk.Check;

            if (isNew) {
                Data.Frames.Add(frame);
            } else {
                Data.Frames[Data.Frames.Count - 1] = frame;
            }

            MInput.Disabled = inputDisabled;

        }

    }
}
