using Microsoft.Xna.Framework.Input;
using MonoMod;
using System.Collections.Generic;

namespace Monocle {
    [MonoModPatch("VirtualButton")]
    public class patch_VirtualButton : VirtualButton {

        public List<Node> Nodes;

#pragma warning disable CS0649 // field staying default
        [MonoModIgnore]
        private float firstRepeatTime;
        [MonoModIgnore]
        private float multiRepeatTime;
        [MonoModIgnore]
        private float bufferCounter;
        [MonoModIgnore]
        private float repeatCounter;
        [MonoModIgnore]
        private bool canRepeat;
        [MonoModIgnore]
        private bool consumed;
        [MonoModIgnore]
        public new bool Repeating { get; private set; }
#pragma warning restore CS0649

        public bool AutoConsumeBuffer;

        public new bool Check {
            [MonoModReplace]
            get {
                if (MInput.Disabled)
                    return false;

                if (Binding.Check(GamepadIndex, Threshold))
                    return true;

                foreach (Node node in Nodes)
                    if (node.Check)
                        return true;

                return false;
            }
        }

        public new bool Pressed {
            [MonoModReplace]
            get {
                if (DebugOverridePressed.HasValue && MInput.Keyboard.Check(DebugOverridePressed.Value))
                    return true;

                if (MInput.Disabled)
                    return false;

                if (consumed)
                    return false;

                if (bufferCounter > 0f || Repeating) {
                    if (AutoConsumeBuffer)
                        bufferCounter = 0f;
                    return true;
                }

                if (Binding.Pressed(GamepadIndex, Threshold)) {
                    if (AutoConsumeBuffer)
                        bufferCounter = 0f;
                    return true;
                }

                foreach (Node node in Nodes) {
                    if (node.Pressed) {
                        if (AutoConsumeBuffer)
                            bufferCounter = 0f;
                        return true;
                    }
                }

                return false;
            }
        }

        public new bool Released {
            [MonoModReplace]
            get {
                if (MInput.Disabled)
                    return false;

                if (Binding.Released(GamepadIndex, Threshold))
                    return true;

                foreach (Node node in Nodes)
                    if (node.Released)
                        return true;

                return false;
            }
        }

        public patch_VirtualButton() {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public patch_VirtualButton(float bufferTime) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public patch_VirtualButton(float bufferTime, params Node[] nodes) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public patch_VirtualButton(params Node[] nodes) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }


#pragma warning disable CS0626 // method is external and has no attribute
        public extern void orig_ctor();
        public extern void orig_ctor(Binding binding, int gamepadIndex, float bufferTime, float triggerThreshold);
#pragma warning restore CS0626


        [MonoModConstructor]
        public void ctor() {
            orig_ctor();
            Binding = Binding ?? new Binding();
            Nodes = new List<Node>();
        }

        [MonoModConstructor]
        public void ctor(Binding binding, int gamepadIndex, float bufferTime, float triggerThreshold) {
            orig_ctor(binding, gamepadIndex, bufferTime, triggerThreshold);
            Nodes = new List<Node>();
        }

        [MonoModConstructor]
        public void ctor(float bufferTime) {
            ctor(new Binding(), 0, bufferTime, 0f);
        }

        [MonoModConstructor]
        public void ctor(float bufferTime, params Node[] nodes) {
            ctor(new Binding(), 0, bufferTime, 0f);
            Nodes.AddRange(nodes);
        }

        [MonoModConstructor]
        public void ctor(params Node[] nodes) {
            ctor(new Binding(), 0, 0f, 0f);
            Nodes.AddRange(nodes);
        }

        [MonoModReplace]
        public override void Update() {
            consumed = false;
            bufferCounter -= Engine.DeltaTime;

            bool down = false;

            if (Binding.Pressed(GamepadIndex, Threshold)) {
                bufferCounter = BufferTime;
                down = true;
            } else if (Binding.Check(GamepadIndex, Threshold)) {
                down = true;
            }

            foreach (Node node in Nodes) {
                node.Update();
                if (node.Pressed) {
                    if (node.Bufferable) {
                        bufferCounter = BufferTime;
                    }
                    down = true;
                } else if (node.Check) {
                    down = true;
                }
            }

            if (!down) {
                Repeating = false;
                repeatCounter = 0f;
                bufferCounter = 0f;
                return;
            }
            
            if (!canRepeat)
                return;

            Repeating = false;

            if (repeatCounter == 0f) {
                repeatCounter = firstRepeatTime;
                return;
            }

            repeatCounter -= Engine.DeltaTime;

            if (repeatCounter <= 0f) {
                Repeating = true;
                repeatCounter = multiRepeatTime;
            }
        }



        // Copied from Celeste 1.3.3.11

        public abstract class Node : VirtualInputNode {
            public abstract bool Check { get; }
            public abstract bool Pressed { get; }
            public abstract bool Released { get; }

            // ... except for this.
            public virtual bool Bufferable { get; set; }
        }

        public class KeyboardKey : Node {
            public Keys Key;

            public override bool Check => MInput.Keyboard.Check(Key);

            public override bool Pressed => MInput.Keyboard.Pressed(Key);

            public override bool Released => MInput.Keyboard.Released(Key);

            public KeyboardKey(Keys key) {
                Key = key;
            }
        }

        public class PadButton : Node {
            public int GamepadIndex;

            public Buttons Button;

            public override bool Check => MInput.GamePads[GamepadIndex].Check(Button);

            public override bool Pressed => MInput.GamePads[GamepadIndex].Pressed(Button);

            public override bool Released => MInput.GamePads[GamepadIndex].Released(Button);

            public PadButton(int gamepadIndex, Buttons button) {
                GamepadIndex = gamepadIndex;
                Button = button;
            }
        }

        public class PadLeftStickRight : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override bool Check => MInput.GamePads[GamepadIndex].LeftStickRightCheck(Deadzone);

            public override bool Pressed => MInput.GamePads[GamepadIndex].LeftStickRightPressed(Deadzone);

            public override bool Released => MInput.GamePads[GamepadIndex].LeftStickRightReleased(Deadzone);

            public PadLeftStickRight(int gamepadindex, float deadzone) {
                GamepadIndex = gamepadindex;
                Deadzone = deadzone;
            }
        }

        public class PadLeftStickLeft : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override bool Check => MInput.GamePads[GamepadIndex].LeftStickLeftCheck(Deadzone);

            public override bool Pressed => MInput.GamePads[GamepadIndex].LeftStickLeftPressed(Deadzone);

            public override bool Released => MInput.GamePads[GamepadIndex].LeftStickLeftReleased(Deadzone);

            public PadLeftStickLeft(int gamepadindex, float deadzone) {
                GamepadIndex = gamepadindex;
                Deadzone = deadzone;
            }
        }

        public class PadLeftStickUp : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override bool Check => MInput.GamePads[GamepadIndex].LeftStickUpCheck(Deadzone);

            public override bool Pressed => MInput.GamePads[GamepadIndex].LeftStickUpPressed(Deadzone);

            public override bool Released => MInput.GamePads[GamepadIndex].LeftStickUpReleased(Deadzone);

            public PadLeftStickUp(int gamepadindex, float deadzone) {
                GamepadIndex = gamepadindex;
                Deadzone = deadzone;
            }
        }

        public class PadLeftStickDown : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override bool Check => MInput.GamePads[GamepadIndex].LeftStickDownCheck(Deadzone);

            public override bool Pressed => MInput.GamePads[GamepadIndex].LeftStickDownPressed(Deadzone);

            public override bool Released => MInput.GamePads[GamepadIndex].LeftStickDownReleased(Deadzone);

            public PadLeftStickDown(int gamepadindex, float deadzone) {
                GamepadIndex = gamepadindex;
                Deadzone = deadzone;
            }
        }

        public class PadRightStickRight : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override bool Check => MInput.GamePads[GamepadIndex].RightStickRightCheck(Deadzone);

            public override bool Pressed => MInput.GamePads[GamepadIndex].RightStickRightPressed(Deadzone);

            public override bool Released => MInput.GamePads[GamepadIndex].RightStickRightReleased(Deadzone);

            public PadRightStickRight(int gamepadindex, float deadzone) {
                GamepadIndex = gamepadindex;
                Deadzone = deadzone;
            }
        }

        public class PadRightStickLeft : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override bool Check => MInput.GamePads[GamepadIndex].RightStickLeftCheck(Deadzone);

            public override bool Pressed => MInput.GamePads[GamepadIndex].RightStickLeftPressed(Deadzone);

            public override bool Released => MInput.GamePads[GamepadIndex].RightStickLeftReleased(Deadzone);

            public PadRightStickLeft(int gamepadindex, float deadzone) {
                GamepadIndex = gamepadindex;
                Deadzone = deadzone;
            }
        }

        public class PadRightStickUp : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override bool Check => MInput.GamePads[GamepadIndex].RightStickUpCheck(Deadzone);

            public override bool Pressed => MInput.GamePads[GamepadIndex].RightStickUpPressed(Deadzone);

            public override bool Released => MInput.GamePads[GamepadIndex].RightStickUpReleased(Deadzone);

            public PadRightStickUp(int gamepadindex, float deadzone) {
                GamepadIndex = gamepadindex;
                Deadzone = deadzone;
            }
        }

        public class PadRightStickDown : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override bool Check => MInput.GamePads[GamepadIndex].RightStickDownCheck(Deadzone);

            public override bool Pressed => MInput.GamePads[GamepadIndex].RightStickDownPressed(Deadzone);

            public override bool Released => MInput.GamePads[GamepadIndex].RightStickDownReleased(Deadzone);

            public PadRightStickDown(int gamepadindex, float deadzone) {
                GamepadIndex = gamepadindex;
                Deadzone = deadzone;
            }
        }

        public class PadLeftTrigger : Node {
            public int GamepadIndex;

            public float Threshold;

            public override bool Check => MInput.GamePads[GamepadIndex].LeftTriggerCheck(Threshold);

            public override bool Pressed => MInput.GamePads[GamepadIndex].LeftTriggerPressed(Threshold);

            public override bool Released => MInput.GamePads[GamepadIndex].LeftTriggerReleased(Threshold);

            public PadLeftTrigger(int gamepadIndex, float threshold) {
                GamepadIndex = gamepadIndex;
                Threshold = threshold;
            }
        }

        public class PadRightTrigger : Node {
            public int GamepadIndex;

            public float Threshold;

            public override bool Check => MInput.GamePads[GamepadIndex].RightTriggerCheck(Threshold);

            public override bool Pressed => MInput.GamePads[GamepadIndex].RightTriggerPressed(Threshold);

            public override bool Released => MInput.GamePads[GamepadIndex].RightTriggerReleased(Threshold);

            public PadRightTrigger(int gamepadIndex, float threshold) {
                GamepadIndex = gamepadIndex;
                Threshold = threshold;
            }
        }

        public class PadDPadRight : Node {
            public int GamepadIndex;

            public override bool Check => MInput.GamePads[GamepadIndex].DPadRightCheck;

            public override bool Pressed => MInput.GamePads[GamepadIndex].DPadRightPressed;

            public override bool Released => MInput.GamePads[GamepadIndex].DPadRightReleased;

            public PadDPadRight(int gamepadIndex) {
                GamepadIndex = gamepadIndex;
            }
        }

        public class PadDPadLeft : Node {
            public int GamepadIndex;

            public override bool Check => MInput.GamePads[GamepadIndex].DPadLeftCheck;

            public override bool Pressed => MInput.GamePads[GamepadIndex].DPadLeftPressed;

            public override bool Released => MInput.GamePads[GamepadIndex].DPadLeftReleased;

            public PadDPadLeft(int gamepadIndex) {
                GamepadIndex = gamepadIndex;
            }
        }

        public class PadDPadUp : Node {
            public int GamepadIndex;

            public override bool Check => MInput.GamePads[GamepadIndex].DPadUpCheck;

            public override bool Pressed => MInput.GamePads[GamepadIndex].DPadUpPressed;

            public override bool Released => MInput.GamePads[GamepadIndex].DPadUpReleased;

            public PadDPadUp(int gamepadIndex) {
                GamepadIndex = gamepadIndex;
            }
        }

        public class PadDPadDown : Node {
            public int GamepadIndex;

            public override bool Check => MInput.GamePads[GamepadIndex].DPadDownCheck;

            public override bool Pressed => MInput.GamePads[GamepadIndex].DPadDownPressed;

            public override bool Released => MInput.GamePads[GamepadIndex].DPadDownReleased;

            public PadDPadDown(int gamepadIndex) {
                GamepadIndex = gamepadIndex;
            }
        }

        public class MouseLeftButton : Node {
            public override bool Check => MInput.Mouse.CheckLeftButton;

            public override bool Pressed => MInput.Mouse.PressedLeftButton;

            public override bool Released => MInput.Mouse.ReleasedLeftButton;
        }

        public class MouseRightButton : Node {
            public override bool Check => MInput.Mouse.CheckRightButton;

            public override bool Pressed => MInput.Mouse.PressedRightButton;

            public override bool Released => MInput.Mouse.ReleasedRightButton;
        }

        public class MouseMiddleButton : Node {
            public override bool Check => MInput.Mouse.CheckMiddleButton;

            public override bool Pressed => MInput.Mouse.PressedMiddleButton;

            public override bool Released => MInput.Mouse.ReleasedMiddleButton;
        }

        public class VirtualAxisTrigger : Node {
            public enum Modes {
                LargerThan,
                LessThan,
                Equals
            }

            public ThresholdModes Mode;

            public float Threshold;

            private VirtualAxis axis;

            public override bool Check {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return axis.Value >= Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return axis.Value <= Threshold;

                    return axis.Value == Threshold;
                }
            }

            public override bool Pressed {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return axis.Value >= Threshold && axis.PreviousValue < Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return axis.Value <= Threshold && axis.PreviousValue > Threshold;

                    return axis.Value == Threshold && axis.PreviousValue != Threshold;
                }
            }

            public override bool Released {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return axis.Value < Threshold && axis.PreviousValue >= Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return axis.Value > Threshold && axis.PreviousValue <= Threshold;

                    return axis.Value != Threshold && axis.PreviousValue == Threshold;
                }
            }

            public VirtualAxisTrigger(VirtualAxis axis, ThresholdModes mode, float threshold) {
                this.axis = axis;
                Mode = mode;
                Threshold = threshold;
            }
        }

        public class VirtualIntegerAxisTrigger : Node {
            public enum Modes {
                LargerThan,
                LessThan,
                Equals
            }

            public ThresholdModes Mode;

            public int Threshold;

            private VirtualIntegerAxis axis;

            public override bool Check {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return axis.Value >= Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return axis.Value <= Threshold;

                    return axis.Value == Threshold;
                }
            }

            public override bool Pressed {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return axis.Value >= Threshold && axis.PreviousValue < Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return axis.Value <= Threshold && axis.PreviousValue > Threshold;

                    return axis.Value == Threshold && axis.PreviousValue != Threshold;
                }
            }

            public override bool Released {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return axis.Value < Threshold && axis.PreviousValue >= Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return axis.Value > Threshold && axis.PreviousValue <= Threshold;

                    return axis.Value != Threshold && axis.PreviousValue == Threshold;
                }
            }

            public VirtualIntegerAxisTrigger(VirtualIntegerAxis axis, ThresholdModes mode, int threshold) {
                this.axis = axis;
                Mode = mode;
                Threshold = threshold;
            }
        }

        public class VirtualJoystickXTrigger : Node {
            public enum Modes {
                LargerThan,
                LessThan,
                Equals
            }

            public ThresholdModes Mode;

            public float Threshold;

            private VirtualJoystick joystick;

            public override bool Check {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return joystick.Value.X >= Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return joystick.Value.X <= Threshold;

                    return joystick.Value.X == Threshold;
                }
            }

            public override bool Pressed {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return joystick.Value.X >= Threshold && joystick.PreviousValue.X < Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return joystick.Value.X <= Threshold && joystick.PreviousValue.X > Threshold;

                    return joystick.Value.X == Threshold && joystick.PreviousValue.X != Threshold;
                }
            }

            public override bool Released {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                            return joystick.Value.X < Threshold && joystick.PreviousValue.X >= Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return joystick.Value.X > Threshold && joystick.PreviousValue.X <= Threshold;

                    return joystick.Value.X != Threshold && joystick.PreviousValue.X == Threshold;
                }
            }

            public VirtualJoystickXTrigger(VirtualJoystick joystick, ThresholdModes mode, float threshold) {
                this.joystick = joystick;
                Mode = mode;
                Threshold = threshold;
            }
        }

        // 1.3.3.11's VirtualJoystickYTrigger is virtually identical to the VirtualJoystickXTrigger.
        // This could be a copy-pasta bug, or it could be intentional.

        public class VirtualJoystickYTrigger : Node {
            public ThresholdModes Mode;

            public float Threshold;

            private VirtualJoystick joystick;

            public override bool Check {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return joystick.Value.X >= Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return joystick.Value.X <= Threshold;

                    return joystick.Value.X == Threshold;
                }
            }

            public override bool Pressed {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return joystick.Value.X >= Threshold && joystick.PreviousValue.X < Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return joystick.Value.X <= Threshold && joystick.PreviousValue.X > Threshold;

                    return joystick.Value.X == Threshold && joystick.PreviousValue.X != Threshold;
                }
            }

            public override bool Released {
                get {
                    if (Mode == ThresholdModes.LargerThan)
                        return joystick.Value.X < Threshold && joystick.PreviousValue.X >= Threshold;

                    if (Mode == ThresholdModes.LessThan)
                        return joystick.Value.X > Threshold && joystick.PreviousValue.X <= Threshold;

                    return joystick.Value.X != Threshold && joystick.PreviousValue.X == Threshold;
                }
            }

            public VirtualJoystickYTrigger(VirtualJoystick joystick, ThresholdModes mode, float threshold) {
                this.joystick = joystick;
                Mode = mode;
                Threshold = threshold;
            }
        }

    }
}
