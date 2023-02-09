using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    public static class ILShims {
        [RelinkLegacyMonoMod("Mono.Cecil.Cil.Instruction MonoMod.Cil.ILLabel::Target")]
        public static Instruction ILLabel_GetTarget(ILLabel label) => label.Target; // This previously used to be a field
    }
}