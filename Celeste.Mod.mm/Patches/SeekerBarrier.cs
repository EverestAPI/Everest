using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;

namespace Celeste {
    public class patch_SeekerBarrier : SeekerBarrier {
        public patch_SeekerBarrier(EntityData data, Vector2 offset) : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchSeekerBarrierDraw]
        public override extern void Render();

        private bool IsVisible() => CullHelper.IsRectangleVisible(Position.X, Position.Y, Width, Height);
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to implement culling.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchSeekerBarrierDraw))]
    class PatchSeekerBarrierDraw : Attribute { }

    static partial class MonoModRules {
        public static void PatchSeekerBarrierDraw(ILContext il, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(il);

            // Insert this code at the start of the method:
            // if (!IsVisible())
            //     return;

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Call, il.Method.DeclaringType.FindMethod("System.Boolean IsVisible()"));

            // return early if IsVisible returned false
            ILLabel label = cursor.DefineLabel();
            cursor.Emit(OpCodes.Brtrue, label);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(label);
        }
    }
}