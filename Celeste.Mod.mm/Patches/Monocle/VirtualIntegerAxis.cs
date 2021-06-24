using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using Node = Monocle.VirtualAxis.Node;

namespace Monocle {
    public class patch_VirtualIntegerAxis : VirtualIntegerAxis {

        public List<Node> Nodes;

        public patch_VirtualIntegerAxis() {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public patch_VirtualIntegerAxis(params Node[] nodes) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }


#pragma warning disable CS0626 // method is external and has no attribute
        public extern void orig_ctor();
        public extern void orig_ctor(Binding negative, Binding positive, int gamepadIndex, float threshold, OverlapBehaviors overlapBehavior = OverlapBehaviors.TakeNewer);
        public extern void orig_ctor(Binding negative, Binding negativeAlt, Binding positive, Binding positiveAlt, int gamepadIndex, float threshold, OverlapBehaviors overlapBehavior = OverlapBehaviors.TakeNewer);
#pragma warning restore CS0626


        [MonoModConstructor]
        public void ctor() {
            orig_ctor();
            Negative = Negative ?? new Binding();
            Positive = Positive ?? new Binding();
            Nodes = new List<Node>();
        }

        [MonoModConstructor]
        public void ctor(Binding negative, Binding positive, int gamepadIndex, float threshold, OverlapBehaviors overlapBehavior = OverlapBehaviors.TakeNewer) {
            orig_ctor(negative, positive, gamepadIndex, threshold, overlapBehavior);
            Nodes = new List<Node>();
        }

        [MonoModConstructor]
        public void ctor(Binding negative, Binding negativeAlt, Binding positive, Binding positiveAlt, int gamepadIndex, float threshold, OverlapBehaviors overlapBehavior = OverlapBehaviors.TakeNewer) {
            orig_ctor(negative, negativeAlt, positive, positiveAlt, gamepadIndex, threshold, overlapBehavior);
            Nodes = new List<Node>();
        }

        [MonoModConstructor]
        public void ctor(params Node[] nodes) {
            ctor(new Binding(), new Binding(), 0, 0f, OverlapBehaviors.TakeNewer);
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
                    float value = node.Value;
                    if (value != 0f) {
                        Value = Math.Sign(value);
                        if (Inverted)
                            Value *= -1;
                        break;
                    }
                }
            }
        }

        public void CheckBinds(out bool pos, out bool neg) {
            pos = Positive.Axis(GamepadIndex, Threshold) > 0f;
            neg = Negative.Axis(GamepadIndex, Threshold) > 0f;
        }

        public static implicit operator int(patch_VirtualIntegerAxis axis) => axis.Value;

    }
}
