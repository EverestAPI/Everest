using Mono.Cecil;
using MonoMod;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using CustomAttributeNamedArgument = Mono.Cecil.CustomAttributeNamedArgument;

namespace NETCoreifier {
    public static class Coreifier {

        public static void ConvertToNetCore(MonoModder modder)
            => ConvertToNetCore(modder.Module);

        public static void ConvertToNetCore(ModuleDefinition module) {
            module.RuntimeVersion = System.Reflection.Assembly.GetCallingAssembly().ImageRuntimeVersion;

            // Clear 32 bit flag
            module.Attributes &= ~ModuleAttributes.Required32Bit;

            // Patch target framework attribute
            TargetFrameworkAttribute attr = System.Reflection.Assembly.GetCallingAssembly().GetCustomAttribute<TargetFrameworkAttribute>();
            CustomAttribute moduleAttr = module.Assembly.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == typeof(TargetFrameworkAttribute).FullName);
            if (moduleAttr != null) {
                if (((string) moduleAttr.ConstructorArguments[0].Value).StartsWith(".NETFramework"))
                    PatchFrameworkModule(module);

                if (attr != null) {
                    moduleAttr.ConstructorArguments[0] = new CustomAttributeArgument(module.ImportReference(typeof(string)), attr.FrameworkName);
                    moduleAttr.Properties.Clear();
                    moduleAttr.Properties.Add(new CustomAttributeNamedArgument(nameof(attr.FrameworkDisplayName), new CustomAttributeArgument(module.ImportReference(typeof(string)), attr.FrameworkDisplayName)));
                }
            }
        }

        private static void PatchFrameworkModule(ModuleDefinition module) {
            //TODO Patch RNG
        }

    }
}