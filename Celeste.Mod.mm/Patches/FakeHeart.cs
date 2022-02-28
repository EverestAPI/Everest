using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    public class patch_FakeHeart : FakeHeart {
        public patch_FakeHeart(Vector2 position)
            : base(position) {
            // dummy constructor
        }

        // -1 is the vanilla (random) color.
        private AreaMode color;

        [MonoModConstructor]
        [MonoModIgnore]
        public extern void ctor(Vector2 position);

        [MonoModConstructor]
        [MonoModReplace]
        public void ctor(EntityData data, Vector2 offset) {
            ctor(data.Position + offset);

            color = data.Enum<AreaMode>("color", (AreaMode) (-1));
        }

        [MonoModIgnore]
        [PatchFakeHeartColor] // adds a call to _getCustomColor to override the random color
        public extern override void Awake(Scene scene);

        private AreaMode _GetCustomColor(AreaMode vanillaColor) {
            return color != (AreaMode) (-1) ? color : vanillaColor;
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the fake heart color to make it customizable.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFakeHeartColor))]
    class PatchFakeHeartColorAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchFakeHeartColor(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_getCustomColor = context.Method.DeclaringType.FindMethod("Celeste.AreaMode Celeste.FakeHeart::_GetCustomColor(Celeste.AreaMode)");

            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(instr => instr.MatchLdsfld("Monocle.Calc", "Random"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.GotoNext(MoveType.After, instr => instr.MatchCall("Monocle.Calc", "Choose"));
            cursor.Emit(OpCodes.Call, m_getCustomColor);
        }

    }
}
