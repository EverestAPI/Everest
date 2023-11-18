using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using System;

namespace Celeste {
    class patch_LavaRect : LavaRect {

        public patch_LavaRect(float width, float height, int step) : base(width, height, step) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchLavaRectResize]
        public extern new void Resize(float width, float height, int step);

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch LavaRect.Resize to perform calculations using doubles instead of floats, fixing float jank on .NET Core Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLavaRectResize))]
    class PatchLavaRectResizeAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchLavaRectResize(ILContext context, CustomAttribute attrib) {
            for (ILCursor cursor = new ILCursor(context); cursor.Next != null; cursor.Index++) {
                Instruction instr = cursor.Next;
                if (instr.MatchLdcR4(out float v)) {
                    // ldc.r4 <constant> -> ...; conv.r8
                    cursor.Index++;
                    cursor.Emit(OpCodes.Conv_R8);
                    cursor.Index--;
                } else if (instr.MatchConvR4()) {
                    // conv.r4 -> conv.r8
                    cursor.Remove();
                    cursor.Emit(OpCodes.Conv_R8);
                } else if (instr.MatchLdarg(out int idx)) {
                    // ldarg <float arg> -> ...; conv.r8
                    if (idx == 0 || context.Method.Parameters[idx-1].ParameterType.MetadataType != MetadataType.Single)
                        continue;

                    cursor.Index++;
                    cursor.Emit(OpCodes.Conv_R8);
                    cursor.Index--;
                } else if (instr.MatchCall(out MethodReference methodRef)) {
                    // Adjust the function signature to use doubles instead of floats
                    MethodReference adjRef = new MethodReference(methodRef.Name, methodRef.ReturnType, methodRef.DeclaringType);

                    if (adjRef.ReturnType.MetadataType == MetadataType.Single)
                        adjRef.ReturnType = context.Module.TypeSystem.Double;

                    foreach (ParameterDefinition param in methodRef.Parameters) {
                        if (param.ParameterType.MetadataType != MetadataType.Single)
                            adjRef.Parameters.Add(param);
                        else
                            adjRef.Parameters.Add(new ParameterDefinition(context.Module.TypeSystem.Double));
                    }

                    instr.Operand = adjRef;
                } else if (instr.MatchBr(out _))
                    // Abort once we hit the loop
                    break;
            }
        }

    }
}