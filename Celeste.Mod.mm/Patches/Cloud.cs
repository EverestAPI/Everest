using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_Cloud : Cloud {

        public bool? Small;

        public patch_Cloud(Vector2 position, bool fragile)
            : base(position, fragile) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchCloudAdded] // ... except for manually manipulating the method via MonoModRules
        public extern new void Added(Scene scene);

        public bool IsSmall(bool value) => Small ?? value;

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the Cloud.Added method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCloudAdded))]
    class PatchCloudAddedAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchCloudAdded(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_IsSmall = context.Method.DeclaringType.FindMethod("System.Boolean Celeste.Cloud::IsSmall(System.Boolean)");

            ILCursor cursor = new ILCursor(context);

            cursor.GotoNext(instr => instr.MatchCall(out MethodReference m) && m.Name == "SceneAs");
            // Push `this`, to be used later by `IsSmall`
            cursor.Emit(OpCodes.Ldarg_0);

            /* We expect something similar enough to the following:
            ldfld    Celeste.AreaMode Celeste.AreaKey::Mode
            brfalse.s    // We're here
            */
            // We want to be BEFORE !=
            cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.AreaKey", "Mode") &&
                instr.Next.OpCode == OpCodes.Brfalse_S);

            // Process.
            cursor.Emit(OpCodes.Call, m_IsSmall);
        }

    }
}
