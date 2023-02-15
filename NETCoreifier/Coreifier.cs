using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using CustomAttributeNamedArgument = Mono.Cecil.CustomAttributeNamedArgument;

namespace NETCoreifier {
    public static class Coreifier {

        public static void ConvertToNetCore(MonoModder modder, bool sharedDeps = false)
            => ConvertToNetCore(modder.Module, modder.AssemblyResolver, sharedDeps);

        public static void ConvertToNetCore(string inputAsm, string outputAsm = null) {
            ModuleDefinition module = null;;
            try {
                // Read the module
                ReaderParameters readerParams = new ReaderParameters()  { ReadSymbols = true };
                try {
                    module = ModuleDefinition.ReadModule(inputAsm, readerParams);
                } catch (SymbolsNotFoundException) {
                    readerParams.ReadSymbols = false;
                    module = ModuleDefinition.ReadModule(inputAsm, readerParams);
                } catch (SymbolsNotMatchingException) {
                    readerParams.ReadSymbols = false;
                    module = ModuleDefinition.ReadModule(inputAsm, readerParams);
                }

                // Convert the module
                ConvertToNetCore(module);

                // Write the converted module
                module.Write(outputAsm ?? inputAsm, new WriterParameters() { WriteSymbols = readerParams.ReadSymbols });
            } finally {
                module?.Dispose();
            }
        }

        public static void ConvertToNetCore(ModuleDefinition module, IAssemblyResolver asmResolver = null, bool sharedDeps = false) {
            module.RuntimeVersion = System.Reflection.Assembly.GetExecutingAssembly().ImageRuntimeVersion;

            // Clear 32 bit flags
            module.Attributes &= ~(ModuleAttributes.Required32Bit | ModuleAttributes.Preferred32Bit);

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

                    if (asmResolver != null) {
                        modder.AssemblyResolver = asmResolver;
                        modder.SharedAssemblyResolver = true;
                    } else
                        modder.SharedAssemblyResolver = false;
                    modder.MissingDependencyThrow = false;

                    modder.SharedDependencies = sharedDeps;

                    modder.MapDependencies();
                    modder.AutoPatch();
                }
            }
        }

    }
}