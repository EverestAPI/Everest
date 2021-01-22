using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System.Collections.Generic;

namespace Monocle {
    [MonoModIfFlag("V2:Input")]
    [MonoModPatch("VirtualJoystick")]
    public class patch_VirtualJoystick_InputV2 : VirtualJoystick {

        public List<Node> Nodes;

        public bool Normalized;
        public float? SnapSlices;

        [MonoModIgnore]
        public new Vector2 Value { get; private set; }

        public patch_VirtualJoystick_InputV2(bool normalized)
            : base(new Binding(), new Binding(), new Binding(), new Binding(), 0, 0f, OverlapBehaviors.TakeNewer) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public patch_VirtualJoystick_InputV2(bool normalized, params Node[] nodes)
            : base(new Binding(), new Binding(), new Binding(), new Binding(), 0, 0f, OverlapBehaviors.TakeNewer) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }


#pragma warning disable CS0626 // method is external and has no attribute
        public extern void orig_ctor(Binding up, Binding down, Binding left, Binding right, int gamepadIndex, float threshold, OverlapBehaviors overlapBehavior = OverlapBehaviors.TakeNewer);
#pragma warning restore CS0626


        [MonoModConstructor]
        public void ctor(Binding up, Binding down, Binding left, Binding right, int gamepadIndex, float threshold, OverlapBehaviors overlapBehavior = OverlapBehaviors.TakeNewer) {
            orig_ctor(up, down, left, right, gamepadIndex, threshold, overlapBehavior);
            Nodes = new List<Node>();
        }

        [MonoModConstructor]
        public void ctor(bool normalized) {
            ctor(new Binding(), new Binding(), new Binding(), new Binding(), 0, 0f, OverlapBehaviors.TakeNewer);
            Normalized = normalized;
        }

        [MonoModConstructor]
        public void ctor(bool normalized, params Node[] nodes) {
            ctor(normalized);
            Nodes.AddRange(nodes);
        }

#pragma warning disable CS0626 // method is external and has no attribute
        public extern void orig_Update();
#pragma warning restore CS0626
        public override void Update() {
            foreach (Node node in Nodes)
                node.Update();

            orig_Update();

            if (!MInput.Disabled) {
                foreach (Node node in Nodes) {
                    Vector2 value = node.Value;
                    if (value != default) {
                        if (Normalized) {
                            if (SnapSlices.HasValue) {
                                value = value.SnappedNormal(SnapSlices.Value);
                            } else {
                                value.Normalize();
                            }
                        } else if (SnapSlices.HasValue) {
                            value = value.Snapped(SnapSlices.Value);
                        }
                        if (InvertedX)
                            value.X *= -1f;
                        if (InvertedY)
                            value.Y *= -1f;
                        Value = value;
                        break;
                    }
                }
            }
        }



        // Copied from Celeste 1.3.3.11

        public abstract class Node : VirtualInputNode {
            public abstract Vector2 Value { get; }
        }

        public class PadLeftStick : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override Vector2 Value => MInput.GamePads[GamepadIndex].GetLeftStick(Deadzone);

            public PadLeftStick(int gamepadIndex, float deadzone) {
                GamepadIndex = gamepadIndex;
                Deadzone = deadzone;
            }
        }

        public class PadRightStick : Node {
            public int GamepadIndex;

            public float Deadzone;

            public override Vector2 Value => MInput.GamePads[GamepadIndex].GetRightStick(Deadzone);

            public PadRightStick(int gamepadIndex, float deadzone) {
                GamepadIndex = gamepadIndex;
                Deadzone = deadzone;
            }
        }

        public class PadDpad : Node {
            public int GamepadIndex;

            public override Vector2 Value {
                get {
                    Vector2 value = new Vector2();

                    if (MInput.GamePads[GamepadIndex].DPadRightCheck)
                        value.X = 1f;
                    else if (MInput.GamePads[GamepadIndex].DPadLeftCheck)
                        value.X = -1f;
                    
                    if (MInput.GamePads[GamepadIndex].DPadDownCheck)
                        value.Y = 1f;
                    else if (MInput.GamePads[GamepadIndex].DPadUpCheck)
                        value.Y = -1f;

                    return value;
                }
            }

            public PadDpad(int gamepadIndex) {
                GamepadIndex = gamepadIndex;
            }
        }

        public class KeyboardKeys : Node {
            public OverlapBehaviors OverlapBehavior;

            public Keys Left;

            public Keys Right;

            public Keys Up;

            public Keys Down;

            private bool turnedX;

            private bool turnedY;

            private Vector2 value;

            public override Vector2 Value => value;

            public KeyboardKeys(OverlapBehaviors overlapBehavior, Keys left, Keys right, Keys up, Keys down) {
                OverlapBehavior = overlapBehavior;
                Left = left;
                Right = right;
                Up = up;
                Down = down;
            }

            public override void Update() {
                if (MInput.Keyboard.Check(Left)) {
                    if (MInput.Keyboard.Check(Right)) {
                        // l && r
                        switch (OverlapBehavior) {
                            default:
                                value.X = 0f;
                                break;
                            case OverlapBehaviors.TakeNewer:
                                if (!turnedX) {
                                    value.X *= -1f;
                                    turnedX = true;
                                }
                                break;
                            case OverlapBehaviors.TakeOlder:
                                break;
                        }

                    } else {
                        // l && !r
                        turnedX = false;
                        value.X = -1f;
                    }

                } else if (MInput.Keyboard.Check(Right)) {
                    // !l && r
                    turnedX = false;
                    value.X = 1f;

                } else {
                    // !l && !r
                    turnedX = false;
                    value.X = 0f;
                }


                if (MInput.Keyboard.Check(Up)) {
                    if (MInput.Keyboard.Check(Down)) {
                        // u & d
                        switch (OverlapBehavior) {
                            case OverlapBehaviors.TakeOlder:
                                break;
                            default:
                                value.Y = 0f;
                                break;
                            case OverlapBehaviors.TakeNewer:
                                if (!turnedY) {
                                    value.Y *= -1f;
                                    turnedY = true;
                                }
                                break;
                        }

                    } else {
                        // u & !d
                        turnedY = false;
                        value.Y = -1f;
                    }

                } else if (MInput.Keyboard.Check(Down)) {
                    // !u & d
                    turnedY = false;
                    value.Y = 1f;

                } else {
                    // !u & !d
                    turnedY = false;
                    value.Y = 0f;
                }
            }
        }

    }
}
