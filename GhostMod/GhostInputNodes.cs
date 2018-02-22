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
    public static class GhostInputNodes {

        public class MoveX : VirtualAxis.Node {
            public GhostInputReplayer Replayer;
            public MoveX(GhostInputReplayer replayer) {
                Replayer = replayer;
            }
            public override float Value => Replayer.Frame.MoveX;
        }

        public class MoveY : VirtualAxis.Node {
            public GhostInputReplayer Replayer;
            public MoveY(GhostInputReplayer replayer) {
                Replayer = replayer;
            }
            public override float Value => Replayer.Frame.MoveY;
        }

        public class Aim : VirtualJoystick.Node {
            public GhostInputReplayer Replayer;
            public Aim(GhostInputReplayer replayer) {
                Replayer = replayer;
            }
            public override Vector2 Value => Replayer.Frame.Aim;
        }

        public class MountainAim : VirtualJoystick.Node {
            public GhostInputReplayer Replayer;
            public MountainAim(GhostInputReplayer replayer) {
                Replayer = replayer;
            }
            public override Vector2 Value => Replayer.Frame.MountainAim;
        }

        public class Button : VirtualButton.Node {
            public GhostInputReplayer Replayer;
            public int Mask;
            public Button(GhostInputReplayer replayer, GhostFrame.ButtonMask mask) {
                Replayer = replayer;
                Mask = (int) mask;
            }
            public override bool Check => !MInput.Disabled && (Replayer.Frame.Buttons & Mask) == Mask;
            public override bool Pressed => !MInput.Disabled && (Replayer.Frame.Buttons & Mask) == Mask && (Replayer.PrevFrame.Buttons & Mask) == 0;
            public override bool Released => !MInput.Disabled && (Replayer.Frame.Buttons & Mask) == 0 && (Replayer.PrevFrame.Buttons & Mask) == Mask;
        }

    }
}