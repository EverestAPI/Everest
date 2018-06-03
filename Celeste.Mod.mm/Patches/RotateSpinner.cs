#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;
using System.IO;
using FMOD.Studio;
using Monocle;
using Celeste.Mod.Meta;

namespace Celeste {
    // : Entity because there's no original Awake method to hook, thus base.Awake must be Entity::Awake.
    class patch_RotateSpinner : Entity {

        // extern get is invalid.
        [MonoModIgnore]
        public float Angle => MathHelper.Lerp(4.712389f, -1.57079637f, Easer(rotationPercent));

        private Vector2 center;
        private float rotationPercent;
        private float length;

        private Vector2 startCenter;
        private Vector2 startPosition;

        public patch_RotateSpinner(EntityData data, Vector2 offset)
            : base(data.Position + offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor_RotateSpinner(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor_RotateSpinner(EntityData data, Vector2 offset) {
            orig_ctor_RotateSpinner(data, offset);

            startCenter = data.Nodes[0] + offset;
            startPosition = data.Position + offset;

            // Celeste originally runs the following line in the constructor:
            // rotationPercent = EaserInverse(Calc.Percent(angle, 4.712389f, -1.57079637f));
            // Unfortunately, that is wrong... but we need to preserve the original value.
            float angle = Calc.Angle(startCenter, startPosition);
            angle = Calc.WrapAngle(angle);
            // Note: Calc.Percent previously ignored subtracting zeroAt from oneAt when dividing.
            // We thus need to feed it back in.
            rotationPercent = EaserInverse(Calc.Percent(angle, 4.712389f, -1.57079637f + 4.712389f));
        }

        // public extern void orig_Awake(Scene scene);
        public override void Awake(Scene scene) {
            // orig_Awake(scene);
            base.Awake(scene);

            MapMeta meta = AreaData.Get((Scene as Level).Session.Area).GetMeta();
            if (meta?.FixRotateSpinnerAngles ?? false) {
                float angle = Calc.Angle(startCenter, startPosition);
                angle = Calc.WrapAngle(angle);
                rotationPercent = EaserInverse(Calc.Percent(angle, -MathHelper.Pi, MathHelper.Pi));
                Position = center + Calc.AngleToVector(Angle, length);
            }
        }

        [MonoModIgnore]
        private extern float Easer(float v);

        [MonoModIgnore]
        private extern float EaserInverse(float v);

    }
}
