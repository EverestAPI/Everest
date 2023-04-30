using Mono.Cecil;
using System;
using System.Linq;

namespace MonoMod {
    /// <summary>
    /// Marks the patch type as targeting one of the game's dependencies 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = true)]
    public class GameDependencyPatchAttribute : Attribute {
        public GameDependencyPatchAttribute(string asmName) {}
    }

    static partial class MonoModRules {

        // Init rules for patching game dependency DLLs
        private static void InitDependencyRules(MonoModder modder) {
            // Remove all rule types which don't explicitly target this dependency
            for (int i = 0; i < RulesModule.Types.Count; i++) {
                TypeDefinition type = RulesModule.Types[i];
                if (type.FullName.StartsWith("MonoMod."))
                    continue;

                CustomAttribute depPatchAttr = type.CustomAttributes.FirstOrDefault(a => a.Constructor.DeclaringType.FullName == "MonoMod.GameDependencyPatchAttribute");
                if (depPatchAttr?.ConstructorArguments?[0].Value is string asmName && asmName == modder.Module.Assembly.Name.Name) {
                    type.CustomAttributes.Remove(depPatchAttr);
                    continue;
                }

                RulesModule.Types.RemoveAt(i--);
            }
        }

        public static void RemoveDependencyPatches() {
            // Remove all rule types which target a dependency
            for (int i = 0; i < RulesModule.Types.Count; i++)
                if (RulesModule.Types[i].CustomAttributes.Any(a => a.Constructor.DeclaringType.FullName == "MonoMod.GameDependencyPatchAttribute"))
                    RulesModule.Types.RemoveAt(i--);
        }

    }
}
