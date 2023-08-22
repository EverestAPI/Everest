using Mono.Cecil;
using MonoMod.InlineRT;
using System;

namespace MonoMod {
    /// <summary>
    /// Links the specified type / method / field / property / etc. to this one if the mod is targeting legacy MonoMod
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class RelinkLegacyMonoMod : Attribute {
        public RelinkLegacyMonoMod(string linkFromName) { }
    }

    static partial class MonoModRules {

        // Init rules for patching mod DLLs
        private static void InitModRules(MonoModder modder) {
            // Relink against FNA
            RelinkAgainstFNA(modder);

            // Determine if the mod uses (legacy) MonoMod
            bool isLegacyMonoMod = true;
            foreach (AssemblyNameReference name in modder.Module.AssemblyReferences) {
                if (name.Name.StartsWith("MonoMod.") && name.Version.Major >= 23) {
                    isLegacyMonoMod = false;
                }
            }

            // If this is legacy MonoMod or a mod that only uses MMHOOK_Celeste, relink against modern MonoMod
            if (isLegacyMonoMod) {
                SetupLegacyMonoModRelinking(modder);
            } else
                isLegacyMonoMod = false;

            MonoModRule.Flag.Set("LegacyMonoMod", isLegacyMonoMod);
        }
    }
}
