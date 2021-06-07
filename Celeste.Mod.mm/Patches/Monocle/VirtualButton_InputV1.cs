#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on 

using Microsoft.Xna.Framework.Input;
using MonoMod;
using System.Collections.Generic;

namespace Monocle {
    [MonoModIgnore]
    [MonoModPatch("VirtualButton")]
    public class patch_VirtualButton_InputV1 : VirtualButton {

        public List<Node> Nodes;

        [MonoModIgnore]
        public abstract class Node : VirtualInputNode {
            public abstract bool Check { get; }
            public abstract bool Pressed { get; }
            public abstract bool Released { get; }
        }

        [MonoModIgnore]
        public class KeyboardKey : Node {
            public Keys Key;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public KeyboardKey(Keys key) {
            }
        }

        [MonoModIgnore]
        public class PadButton : Node {
            public int GamepadIndex;

            public Buttons Button;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadButton(int gamepadIndex, Buttons button) {
            }
        }

        [MonoModIgnore]
        public class PadLeftStickRight : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadLeftStickRight(int gamepadindex, float deadzone) {
            }
        }

        [MonoModIgnore]
        public class PadLeftStickLeft : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadLeftStickLeft(int gamepadindex, float deadzone) {
            }
        }

        [MonoModIgnore]
        public class PadLeftStickUp : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadLeftStickUp(int gamepadindex, float deadzone) {
            }
        }

        [MonoModIgnore]
        public class PadLeftStickDown : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadLeftStickDown(int gamepadindex, float deadzone) {
            }
        }

        [MonoModIgnore]
        public class PadRightStickRight : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadRightStickRight(int gamepadindex, float deadzone) {
            }
        }

        [MonoModIgnore]
        public class PadRightStickLeft : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadRightStickLeft(int gamepadindex, float deadzone) {
            }
        }

        [MonoModIgnore]
        public class PadRightStickUp : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadRightStickUp(int gamepadindex, float deadzone) {
            }
        }

        [MonoModIgnore]
        public class PadRightStickDown : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadRightStickDown(int gamepadindex, float deadzone) {
            }
        }

        [MonoModIgnore]
        public class PadLeftTrigger : Node {
            public int GamepadIndex;

            public float Threshold;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadLeftTrigger(int gamepadIndex, float threshold) {
            }
        }

        [MonoModIgnore]
        public class PadRightTrigger : Node {
            public int GamepadIndex;

            public float Threshold;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadRightTrigger(int gamepadIndex, float threshold) {
            }
        }

        [MonoModIgnore]
        public class PadDPadRight : Node {
            public int GamepadIndex;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadDPadRight(int gamepadIndex) {
            }
        }

        [MonoModIgnore]
        public class PadDPadLeft : Node {
            public int GamepadIndex;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadDPadLeft(int gamepadIndex) {
            }
        }

        [MonoModIgnore]
        public class PadDPadUp : Node {
            public int GamepadIndex;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadDPadUp(int gamepadIndex) {
            }
        }

        [MonoModIgnore]
        public class PadDPadDown : Node {
            public int GamepadIndex;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public PadDPadDown(int gamepadIndex) {
            }
        }

        [MonoModIgnore]
        public class MouseLeftButton : Node {
            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }
        }

        [MonoModIgnore]
        public class MouseRightButton : Node {
            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }
        }

        [MonoModIgnore]
        public class MouseMiddleButton : Node {
            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }
        }

        [MonoModIgnore]
        public class VirtualAxisTrigger : Node {
            public enum Modes {
                LargerThan,
                LessThan,
                Equals
            }

            public ThresholdModes Mode;

            public float Threshold;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public VirtualAxisTrigger(VirtualAxis axis, ThresholdModes mode, float threshold) {
            }
        }

        [MonoModIgnore]
        public class VirtualIntegerAxisTrigger : Node {
            public enum Modes {
                LargerThan,
                LessThan,
                Equals
            }

            public ThresholdModes Mode;

            public int Threshold;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public VirtualIntegerAxisTrigger(VirtualIntegerAxis axis, ThresholdModes mode, int threshold) {
            }
        }

        [MonoModIgnore]
        public class VirtualJoystickXTrigger : Node {
            public enum Modes {
                LargerThan,
                LessThan,
                Equals
            }

            public ThresholdModes Mode;

            public float Threshold;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public VirtualJoystickXTrigger(VirtualJoystick joystick, ThresholdModes mode, float threshold) {
            }
        }

        // 1.3.3.11's VirtualJoystickYTrigger is virtually identical to the VirtualJoystickXTrigger.
        // This could be a copy-pasta bug, or it could be intentional.

        [MonoModIgnore]
        public class VirtualJoystickYTrigger : Node {
            public ThresholdModes Mode;

            public float Threshold;

            public override extern bool Check { get; }
            public override extern bool Pressed { get; }
            public override extern bool Released { get; }

            public VirtualJoystickYTrigger(VirtualJoystick joystick, ThresholdModes mode, float threshold) {
            }
        }

    }
}
