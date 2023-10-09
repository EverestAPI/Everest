using Mono.Cecil;
using System;
using System.Linq;

namespace MonoMod {
    /// <summary>
    /// Marks the patch type as targeting one of the game's dependencies, as well as copying it into the dependency assembly
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = true)]
    public class GameDependencyPatchAttribute : Attribute {
        public GameDependencyPatchAttribute(string asmName) {}
    }

    /// <summary>
    /// Marks the patch type as targeting one of the game's dependencies while still being a part of the regular Celeste assembly
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = true)]
    public class ExternalGameDependencyPatchAttribute : Attribute {
        public ExternalGameDependencyPatchAttribute(string asmName) {}
    }

    static partial class MonoModRules {

        // Init rules for patching game dependency DLLs
        private static void InitDependencyRules(MonoModder modder) {
            // If this is the MMHook assembly, deprecate On./IL. hooks for the Celeste.Mod namespace
            if (modder.Module.Name.StartsWith("MMHOOK_")) {
                MethodReference obsoleteAttrCtor = modder.Module.ImportReference(typeof(ObsoleteAttribute).GetConstructor(new Type[] { typeof(string), typeof(bool) }));
                foreach (TypeDefinition type in modder.Module.Types) {
                    if (!type.Namespace.StartsWith("On.Celeste.Mod") && !type.Namespace.StartsWith("IL.Celeste.Mod"))
                        continue;

                    type.CustomAttributes.Add(new CustomAttribute(obsoleteAttrCtor) {
                        ConstructorArguments = {
                            new CustomAttributeArgument(modder.Module.TypeSystem.String,
                                """
                                Hooks on Everest-internal types (=types in the Celeste.Mod namespace) are deprecated and unsupported.
                                If you have a legitimate need for creating them, please reach out so that it can be added to the official API!
                                Attempting to uses On. / IL. hookgen helpers to create such hooks will fail for Core mods.
                                """.Replace('\n', ' ')
                            ),
                            new CustomAttributeArgument(modder.Module.TypeSystem.Boolean, true)
                        }
                    });
                }
            }

            for (int i = 0; i < RulesModule.Types.Count; i++) {
                TypeDefinition type = RulesModule.Types[i];
                if (type.FullName.StartsWith("MonoMod."))
                    continue;

                // Check if this is a game dependency patch type
                bool CheckDependencyPatchAttribute(string attrName) {
                    bool hasAttr = false;
                    foreach (CustomAttribute attr in type.CustomAttributes.ToArray()) {
                        if (attr.Constructor.DeclaringType.FullName != $"MonoMod.{attrName}") continue;

                        // Check if this attribute is targeting this dependency
                        if (attr.ConstructorArguments?.Count > 0 && attr.ConstructorArguments?[0].Value is string asmName && asmName == modder.Module.Assembly.Name.Name)
                            hasAttr = true;

                        // Remove it regardless
                        type.CustomAttributes.Remove(attr);
                    }
                    return hasAttr;
                }

                if (!CheckDependencyPatchAttribute(nameof(GameDependencyPatchAttribute))) {
                    // The type should not be copied into this dependency's assembly
                    RulesModule.Types.RemoveAt(i--);
                    type.Scope = CelesteAsmRef; // In case this type will still be referenced, we want to pull it in from the main Celeste.dll

                    // If this is an external patch we still want to process its attributes though
                    if (!CheckDependencyPatchAttribute(nameof(ExternalGameDependencyPatchAttribute)))
                        continue;
                }

                // If this type has a RelinkLegacyMonoMod attribute, handle that
                SetupLegacyMonoModRelinking(modder, type);
            }
        }

        public static void RemoveDependencyPatches() {
            for (int i = 0; i < RulesModule.Types.Count; i++) {
                TypeDefinition type = RulesModule.Types[i];

                // Remove all rule types which should be copied into a dependency assembly
                if (type.CustomAttributes.Any(a => a.Constructor.DeclaringType.FullName == $"MonoMod.{nameof(GameDependencyPatchAttribute)}")) {
                    RulesModule.Types.RemoveAt(i--);
                    continue;
                }

                // Remove all external dependency patch attributes
                foreach (CustomAttribute attr in type.CustomAttributes.ToArray())
                    if (attr.Constructor.DeclaringType.FullName == $"MonoMod.{nameof(ExternalGameDependencyPatchAttribute)}")
                        type.CustomAttributes.Remove(attr);
            }
        }

    }
}
