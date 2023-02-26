using Mono.Cecil;
using MonoMod.InlineRT;
using System;

namespace MonoMod {
    /// <summary>
    /// Links the specified type / method / field / property / etc. to this one if the mod is targeting legacy MonoMod
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class RelinkLegacyMonoMod : Attribute {
        public RelinkLegacyMonoMod(string linkFromName) {}
    }

    static partial class MonoModRules {

        // Init rules for patching mod DLLs
        private static void InitModRules(MonoModder modder) {
            // Relink against FNA
            RelinkAgainstFNA(modder);

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
            if (isMonoMod && isLegacyMonoMod) {
                SetupLegacyMonoModRelinking(modder);
            } else
                isLegacyMonoMod = false;

            MonoModRule.Flag.Set("LegacyMonoMod", isLegacyMonoMod);
        }

        public static void SetupLegacyMonoModRelinking(MonoModder modder) {
            modder.Log($"[Celeste.Mod.mm] Relinking legacy MonoMod to glue code layer");

            // Replace assembly references which changed
            ReplaceAssemblyRefs(modder, static asm => asm.Name.Equals("MonoMod"), GetRulesAssemblyRef("MonoMod.Patcher"));

            // Convert all RelinkLegacyMonoMod attributes to MonoModLinkFrom attributes
            static void VisitAttributes(MonoModder modder, ICustomAttributeProvider prov) {
                foreach (CustomAttribute attr in prov.CustomAttributes)
                    if (attr.AttributeType.FullName == "MonoMod.RelinkLegacyMonoMod")
                        // Note: usually MonoMod removes the attribute (which would be bad because the module is shared), but by calling the method directly it doesn't 
                        modder.ParseLinkFrom((MemberReference) prov, attr);
            }

            static void VisitType(MonoModder modder, TypeDefinition type) {
                VisitAttributes(modder, type);

                foreach (MethodDefinition method in type.Methods)
                    VisitAttributes(modder, method);

                foreach (PropertyDefinition prop in type.Properties)
                    VisitAttributes(modder, prop);

                foreach (EventDefinition evt in type.Events)
                    VisitAttributes(modder, evt);

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
