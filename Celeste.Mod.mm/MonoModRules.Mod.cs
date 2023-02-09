using Mono.Cecil;
using MonoMod.InlineRT;
using System;

namespace MonoMod {
    /// <summary>
    /// Links the specified type / method / field / property / etc. to this one if the mod is targeting legacy MonoMod
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class RelinkLegacyMonoMod : Attribute {
        public RelinkLegacyMonoMod(string linkFromName) {}
    }

    static partial class MonoModRules {

        // Init rules for patching mod DLLs
        private static void InitModRules(MonoModder modder) {
            // Determine if the mod uses (legacy) MonoMod
            bool isMonoMod = false, isLegacyMonoMod = true;
            foreach (AssemblyNameReference name in modder.Module.AssemblyReferences) {
                if (name.Name.StartsWith("MonoMod.")) {
                    isMonoMod = true;

                    // MonoMod version numbers are actually date codes - safe to say no legacy build will come out post 2023
                    if (name.Version.Major >= 23)
                        isLegacyMonoMod = false;
                }
            }

            // If this is legacy MonoMod, relink against modern MonoMod
            if (isMonoMod && isLegacyMonoMod && ReplaceAssemblyRefs(modder, static asm => asm.Name.Equals("MonoMod"), GetRulesAssemblyRef("MonoMod.Patcher"))) {
                SetupLegacyMonoModRelinking(modder);
            } else
                isLegacyMonoMod = false;

            MonoModRule.Flag.Set("LegacyMonoMod", isLegacyMonoMod);
        }

        public static void SetupLegacyMonoModRelinking(MonoModder modder) {
            // Convert all RelinkLegacyMonoMod attributes to MonoModLinkFrom attributes
            static void VisitAttributes(MonoModder modder, ICustomAttributeProvider prov) {
                foreach (CustomAttribute attr in prov.CustomAttributes)
                    if (attr.AttributeType.FullName == "MonoMod.RelinkLegacyMonoMod")
                        modder.ParseLinkFrom((MemberReference) prov, attr);
            }

            static void VisitType(MonoModder modder, TypeDefinition type) {
                VisitAttributes(modder, type);

                foreach (MethodDefinition method in type.Methods)
                    VisitAttributes(modder, method);

                foreach (PropertyDefinition prop in type.Properties)
                    VisitAttributes(modder, prop);

                foreach (FieldDefinition field in type.Fields)
                    VisitAttributes(modder, field);

                foreach (TypeDefinition nestedType in type.NestedTypes)
                    VisitType(modder, nestedType);
            }

            foreach (TypeDefinition type in RulesModule.Types)
                VisitType(modder, type);
        }

    }
}
