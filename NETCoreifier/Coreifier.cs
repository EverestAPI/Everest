using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using CustomAttributeNamedArgument = Mono.Cecil.CustomAttributeNamedArgument;

namespace NETCoreifier {
    public static class Coreifier {

        public static void ConvertToNetCore(MonoModder modder, bool sharedDeps = false, bool justDeps = false)
            => ConvertToNetCore(modder.Module, modder.AssemblyResolver, sharedDeps, justDeps);

        public static void ConvertToNetCore(string inputAsm, string outputAsm = null, bool justDeps = false) {
            ModuleDefinition module = null;
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
                ConvertToNetCore(module, justDeps: justDeps);

                // Write the converted module
                module.Write(outputAsm ?? inputAsm, new WriterParameters() { WriteSymbols = readerParams.ReadSymbols });
            } finally {
                module?.Dispose();
            }
        }

        public static void ConvertToNetCore(ModuleDefinition module, IAssemblyResolver asmResolver = null, bool sharedDeps = false, bool justDeps = false) {
            bool isFrameworkModule = false;
            if (!justDeps) {
                module.RuntimeVersion = System.Reflection.Assembly.GetExecutingAssembly().ImageRuntimeVersion;

                // Clear 32 bit flags
                module.Attributes &= ~(ModuleAttributes.Required32Bit | ModuleAttributes.Preferred32Bit);

                // Patch target framework attribute
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
            }

            if (isFrameworkModule || justDeps) {
                if (!justDeps) {
                    // Patch debuggable attribute
                    // We can't get the attribute from our own assembly (because it's a temporary MonoMod one), so get it from the entry assembly (which is MiniInstaller)
                    DebuggableAttribute everestAttr = Assembly.GetEntryAssembly().GetCustomAttribute<DebuggableAttribute>();
                    CustomAttribute celesteAttr = module.Assembly.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == typeof(DebuggableAttribute).FullName);
                    if (celesteAttr != null && everestAttr != null) {
                        celesteAttr.ConstructorArguments[0] = new CustomAttributeArgument(module.ImportReference(typeof(DebuggableAttribute.DebuggingModes)), everestAttr.DebuggingFlags);
                    }
                }

                // Relink legacy framework code
                using (NetFrameworkModder modder = new NetFrameworkModder()) {
                    modder.Module = module;
                    modder.JustDependencies = justDeps;

                    if (asmResolver != null) {
                        modder.AssemblyResolver = asmResolver;
                        modder.SharedAssemblyResolver = true;
                    } else
                        modder.SharedAssemblyResolver = false;
                    modder.MissingDependencyThrow = false;

                    modder.SharedDependencies = sharedDeps;

                    modder.MapDependencies();
                    if (!justDeps)
                        modder.AutoPatch();
                    else
                        modder.PatchRefs();
                }
            }
        }

    }
}