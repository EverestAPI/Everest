using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Utils;
using System.Linq;
using System.Reflection;

namespace NETCoreifier {
    public class NetFrameworkModder : MonoModder {

        // Patching RNG doesn't seem to be required (yet), as .NET Framework and .NET Core share their RNG implementation
        // TODO TAS still desyncs in 6A after getting the B side tape 

        public override void MapDependencies() {
            // Add reference to System.Runtime + NETCoreifier
            if (!Module.AssemblyReferences.Any(asmRef => asmRef.Name == "System.Runtime")) {
                AssemblyName runtimeName = Assembly.GetExecutingAssembly().GetReferencedAssemblies().First(name => name.Name == "System.Runtime");
                Module.AssemblyReferences.Add(new AssemblyNameReference(runtimeName.Name, runtimeName.Version));
            }

            if (!Module.AssemblyReferences.Any(asmRef => asmRef.Name == "NETCoreifier")) {
                AssemblyName coreifierName = Assembly.GetExecutingAssembly().GetName();
                Module.AssemblyReferences.Add(new AssemblyNameReference(coreifierName.Name, coreifierName.Version));
            }

            base.MapDependencies();
        }

        public override ModuleDefinition DefaultMissingDependencyResolver(MonoModder mod, ModuleDefinition main, string name, string fullName) {
            if (name == "NETCoreifier")
                // We have to load our own module again every time because MonoMod messes with it ._.
                if (ModuleDefinition.ReadModule(Assembly.GetExecutingAssembly().Location) is ModuleDefinition coreifierMod)
                    return coreifierMod;

            return base.DefaultMissingDependencyResolver(mod, main, name, fullName);
        }

        public override void AutoPatch() {
            // Parse our own patching rules
            ParseRules(DependencyMap[Module].First(dep => dep.Assembly.Name.Name == "NETCoreifier"));

            base.AutoPatch();
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

        public override void PatchRefsInMethod(MethodDefinition method) {
            base.PatchRefsInMethod(method);

            // The CoreCLR jitter is much more aggressive about inlining, so explicitly force it to not inline
            // The performance penalty isn't that bad, and it makes modding easier
            // TODO Still find a better criteria for this
            if ((method.ImplAttributes & Mono.Cecil.MethodImplAttributes.AggressiveInlining) == 0)
                method.ImplAttributes |= Mono.Cecil.MethodImplAttributes.NoInlining;

            // Resolve uninstantiated generic typeref/def tokens inside of member methods by replacing them with generic type instances
            // CoreCLR seems to be more strict on this, because the faulty IL worked fine on .NET Framwork / Mono
            if (method.DeclaringType.HasGenericParameters && method.Body != null) {
                for (int i = 0; i < method.Body.Instructions.Count; i++) {
                    Instruction instr = method.Body.Instructions[i];

                    if (instr.OpCode == OpCodes.Ldtoken)
                        //ldtoken doesn't have the strict metadata checking
                        continue;

                    if (instr.Operand is TypeReference typeRef && typeRef.SafeResolve() == method.DeclaringType && !typeRef.IsGenericInstance) {
                        GenericInstanceType typeInst = new GenericInstanceType(typeRef);
                        typeInst.GenericArguments.AddRange(method.DeclaringType.GenericParameters);
                        instr.Operand = typeInst;
                    } else if (instr.Operand is MemberReference memberRef && instr.Operand is not TypeReference && memberRef.DeclaringType.SafeResolve() == method.DeclaringType && !memberRef.DeclaringType.IsGenericInstance) {
                        GenericInstanceType typeInst = new GenericInstanceType(memberRef.DeclaringType);
                        typeInst.GenericArguments.AddRange(method.DeclaringType.GenericParameters);
                        memberRef.DeclaringType = typeInst;
                    }
                }
            }
        }

    }
}