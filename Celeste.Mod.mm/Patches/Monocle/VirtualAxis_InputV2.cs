using Microsoft.Xna.Framework.Input;
using MonoMod;
using System.Collections.Generic;

namespace Monocle {
    // Copied from Celeste 1.3.3.11
    // FIXME!!! Update this to use Binding + Nodes instead!
    [MonoModIfFlag("V2:Input")]
    // Original name because this type is missing from 1.3.3.14+, thus MonoModPatch isn't properly respected.
    public class VirtualAxis : VirtualInput {

        public List<Node> Nodes;

        public float Value { get; private set; }

        public float PreviousValue { get; private set; }

        public VirtualAxis() {
            Nodes = new List<Node>();
        }

        public VirtualAxis(params Node[] nodes) {
            Nodes = new List<Node>(nodes);
        }

        public override void Update() {
            foreach (Node node in Nodes)
                node.Update();

            PreviousValue = Value;
            Value = 0f;

            if (!MInput.Disabled) {
                foreach (Node node in Nodes) {
                    float value = node.Value;
                    if (value != 0f) {
                        Value = value;
                        break;
                    }
                }
            }
        }

        public static implicit operator float(VirtualAxis axis) => axis.Value;



        [MonoModIfFlag("V2:Input")]
        public abstract class Node : VirtualInputNode {
            public abstract float Value { get; }
        }

        [MonoModIfFlag("V2:Input")]
        public class PadLeftStickX : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override float Value => Calc.SignThreshold(MInput.GamePads[GamepadIndex].GetLeftStick().X, Deadzone);

            public PadLeftStickX(int gamepadIndex, float deadzone) {
                GamepadIndex = gamepadIndex;
                Deadzone = deadzone;
            }
        }

        [MonoModIfFlag("V2:Input")]
        public class PadLeftStickY : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override float Value => Calc.SignThreshold(MInput.GamePads[GamepadIndex].GetLeftStick().Y, Deadzone);

            public PadLeftStickY(int gamepadIndex, float deadzone) {
                GamepadIndex = gamepadIndex;
                Deadzone = deadzone;
            }
        }

        [MonoModIfFlag("V2:Input")]
        public class PadRightStickX : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override float Value => Calc.SignThreshold(MInput.GamePads[GamepadIndex].GetRightStick().X, Deadzone);

            public PadRightStickX(int gamepadIndex, float deadzone) {
                GamepadIndex = gamepadIndex;
                Deadzone = deadzone;
            }
        }

        [MonoModIfFlag("V2:Input")]
        public class PadRightStickY : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override float Value => Calc.SignThreshold(MInput.GamePads[GamepadIndex].GetRightStick().Y, Deadzone);

            public PadRightStickY(int gamepadIndex, float deadzone) {
                GamepadIndex = gamepadIndex;
                Deadzone = deadzone;
            }
        }

        [MonoModIfFlag("V2:Input")]
        public class PadDpadLeftRight : Node {
            public int GamepadIndex;

            public override float Value {
                get {
                    if (MInput.GamePads[GamepadIndex].DPadRightCheck)
                        return 1f;

                    if (MInput.GamePads[GamepadIndex].DPadLeftCheck)
                        return -1f;

                    return 0f;
                }
            }

            public PadDpadLeftRight(int gamepadIndex) {
                GamepadIndex = gamepadIndex;
            }
        }

        [MonoModIfFlag("V2:Input")]
        public class PadDpadUpDown : Node {
            public int GamepadIndex;

            public override float Value {
                get {
                    if (MInput.GamePads[GamepadIndex].DPadDownCheck)
                        return 1f;

                    if (MInput.GamePads[GamepadIndex].DPadUpCheck)
                        return -1f;

                    return 0f;
                }
            }

            public PadDpadUpDown(int gamepadIndex) {
                GamepadIndex = gamepadIndex;
            }
        }

        [MonoModIfFlag("V2:Input")]
        public class KeyboardKeys : Node {
            public OverlapBehaviors OverlapBehavior;

            public Keys Positive;

            public Keys Negative;

            private float value;

            private bool turned;

            public override float Value => value;

            public KeyboardKeys(OverlapBehaviors overlapBehavior, Keys negative, Keys positive) {
                OverlapBehavior = overlapBehavior;
                Negative = negative;
                Positive = positive;
            }

            public override void Update() {
                if (MInput.Keyboard.Check(Positive)) {
                    if (MInput.Keyboard.Check(Negative)) {
                        // pos && neg
                        switch (OverlapBehavior) {
                            case OverlapBehaviors.TakeOlder:
                                break;
                            default:
                                value = 0f;
                                break;
                            case OverlapBehaviors.TakeNewer:
                                if (!turned) {
                                    value *= -1f;
                                    turned = true;
                                }
                                break;
                        }

                    } else {
                        // pos && !neg
                        turned = false;
                        value = 1f;
                    }

                } else if (MInput.Keyboard.Check(Negative)) {
                    // !pos && neg
                    turned = false;
                    value = -1f;

                } else {
                    // !pos && !neg
                    turned = false;
                    value = 0f;
                }
            }
        }


    }
}
