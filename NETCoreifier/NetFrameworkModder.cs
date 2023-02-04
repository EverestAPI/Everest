using Mono.Cecil;
using Mono.Cecil.Cil;
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

        public override void PatchRefsInMethod(MethodDefinition method) {
            base.PatchRefsInMethod(method);

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