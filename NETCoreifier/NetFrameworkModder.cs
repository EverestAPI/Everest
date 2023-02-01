using Mono.Cecil;
using MonoMod;
using MonoMod.Utils;
using System.Linq;
using System.Reflection;

namespace NETCoreifier {
    public class NetFrameworkModder : MonoModder {

        //TODO Patch RNG

        private ModuleDefinition runtimeMod;

        public override void MapDependencies() {
            // Add reference to System.Runtime
            if (!Module.AssemblyReferences.Any(asmRef => asmRef.Name == "System.Runtime")) {
                AssemblyName runtimeName = Assembly.GetExecutingAssembly().GetReferencedAssemblies().First(name => name.Name == "System.Runtime");
                Module.AssemblyReferences.Add(new AssemblyNameReference(runtimeName.Name, runtimeName.Version));
            }

            runtimeMod = AssemblyResolver.Resolve(Module.AssemblyReferences.First(asmRef => asmRef.Name == "System.Runtime")).MainModule;

            base.MapDependencies();
        }

        public override IMetadataTokenProvider Relinker(IMetadataTokenProvider mtp, IGenericParameterProvider context) {
            if (mtp is TypeReference typeRef && typeRef.FullName.StartsWith("System.") && typeRef.SafeResolve() == null) {
                // Try to resolve the type
                if (FindType(typeRef.FullName) is TypeDefinition sysType) {
                    LogVerbose($"Relinked system type '{typeRef.FullName}' to {typeRef.Module.Name}");
                    return Module.ImportReference(sysType);
                }
            }

            return base.Relinker(mtp, context);
        }

    }
}