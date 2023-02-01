using Mono.Cecil;
using MonoMod;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using CustomAttributeNamedArgument = Mono.Cecil.CustomAttributeNamedArgument;

namespace NETCoreifier {
    public static class Coreifier {

        public static void ConvertToNetCore(MonoModder modder)
            => ConvertToNetCore(modder.Module, modder.AssemblyResolver);

        public static void ConvertToNetCore(string inputAsm, string outputAsm = null) {
            ModuleDefinition module = ModuleDefinition.ReadModule(inputAsm);
            ConvertToNetCore(module);
            module.Write(outputAsm ?? inputAsm);
        }

        public static void ConvertToNetCore(ModuleDefinition module, IAssemblyResolver asmResolver = null) {
            module.RuntimeVersion = System.Reflection.Assembly.GetExecutingAssembly().ImageRuntimeVersion;

            // Clear 32 bit flag
            module.Attributes &= ~ModuleAttributes.Required32Bit;

            // Patch target framework attribute
            bool isFrameworkModule = false;
            TargetFrameworkAttribute attr = System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>();
            CustomAttribute moduleAttr = module.Assembly.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == typeof(TargetFrameworkAttribute).FullName);
            if (moduleAttr != null) {
                if (((string) moduleAttr.ConstructorArguments[0].Value).StartsWith(".NETFramework"))
                    isFrameworkModule = true;

                if (attr != null) {
                    moduleAttr.ConstructorArguments[0] = new CustomAttributeArgument(module.ImportReference(typeof(string)), attr.FrameworkName);
                    moduleAttr.Properties.Clear();
                    moduleAttr.Properties.Add(new CustomAttributeNamedArgument(nameof(attr.FrameworkDisplayName), new CustomAttributeArgument(module.ImportReference(typeof(string)), attr.FrameworkDisplayName)));
                }
            }

            if (isFrameworkModule) {
                // Relink legacy framework code
                using (NetFrameworkModder modder = new NetFrameworkModder()) {
                    modder.Module = module;
                    modder.MissingDependencyThrow = false;
                    modder.AssemblyResolver ??= asmResolver;

                    modder.MapDependencies();
                    modder.AutoPatch();

                    modder.Module = null;
                }
            }
        }

    }
}