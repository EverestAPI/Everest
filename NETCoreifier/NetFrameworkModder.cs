using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace NETCoreifier {
    public class NetFrameworkModder : MonoModder {

        // Patching RNG doesn't seem to be required (yet), as .NET Framework and .NET Core share their RNG implementation

        private static readonly HashSet<string> _PrivateSystemLibs = new HashSet<string>() { "System.Private.CoreLib" };

        public Action<string> LogCallback, LogVerboseCallback;
        public bool SharedAssemblyResolver, SharedDependencies;
        public bool PreventInlining = true;
        private ModuleDefinition _CoreifierModule;

        public override void Dispose() {
            // Don't dispose the main module
            Module = null;

            // Don't dispose the assembly resolver if it's shared
            if (SharedAssemblyResolver)
                AssemblyResolver = null;

            // Don't dispose the dependency modules if they're shared 
            if (SharedDependencies) {
                DependencyMap.Clear();
                DependencyCache.Clear();
            }

            _CoreifierModule?.Dispose();

            base.Dispose();
        }

        public override void Log(string text)
            => LogCallback?.Invoke(text);

        public override void LogVerbose(string text)
            => LogVerboseCallback?.Invoke(text);

        public void AddReferenceIfMissing(AssemblyName asmName) {
            if (!Module.AssemblyReferences.Any(asmRef => asmRef.Name == asmName.Name)) {
                Module.AssemblyReferences.Add(AssemblyNameReference.Parse(asmName.FullName));
            }
        }

        public void AddReferenceIfMissing(string name) => AddReferenceIfMissing(Assembly.GetExecutingAssembly().GetReferencedAssemblies().First(asmName => asmName.Name == name));

        public override void MapDependencies() {
            // Ensure critical references are present
            AddReferenceIfMissing("System.Runtime");
            AddReferenceIfMissing(Assembly.GetExecutingAssembly().GetName());

            // We have to load our own module again every time because MonoMod messes with it ._.
            _CoreifierModule ??= ModuleDefinition.ReadModule(Assembly.GetExecutingAssembly().Location) ?? throw new Exception("Failed to load .NET Coreifier assembly");
            DependencyCache[Assembly.GetExecutingAssembly().FullName] = _CoreifierModule;

            base.MapDependencies();
        }

        public override void AutoPatch() {
            // Parse our own patching rules
            ParseRules(DependencyMap[Module].First(dep => dep.Assembly.Name.Name == "NETCoreifier"));

            base.AutoPatch();
        }

        public override void PatchRefs(ModuleDefinition mod) {
            base.PatchRefs(mod);

            // Remove references to private system libraries
            for (int i = 0; i < mod.AssemblyReferences.Count; i++) {
                if (_PrivateSystemLibs.Contains(mod.AssemblyReferences[i].Name))
                    mod.AssemblyReferences.RemoveAt(i--);
            }
        }

        public override IMetadataTokenProvider Relinker(IMetadataTokenProvider mtp, IGenericParameterProvider context) {
            IMetadataTokenProvider relinkedMtp = base.Relinker(mtp, context);

            if (relinkedMtp is TypeReference typeRef && _PrivateSystemLibs.Contains(typeRef.Scope.Name)) {
                // Don't reference System.Private.CoreLib directly
                return Module.ImportReference(FindType(typeRef.FullName));
            }

            return relinkedMtp;
        }

        public override void PatchRefsInMethod(MethodDefinition method) {
            base.PatchRefsInMethod(method);

            // The CoreCLR JIT is much more aggressive about inlining, so explicitly force it to not inline in some cases
            // The performance penalty isn't that bad, and it makes modding easier
            if (PreventInlining && (method.ImplAttributes & MethodImplAttributes.AggressiveInlining) == 0 && method.Body is MethodBody body && !CanInlineLegacyCode(body))
                method.ImplAttributes |= MethodImplAttributes.NoInlining;

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

        // Use the mono criteria for this, as those are known (see mono_method_check_inlining)
        private bool CanInlineLegacyCode(MethodBody body) {
            const int INLINE_LENGTH_LIMIT = 20; // mono/mini/method-to-ir.c

            // Methods exceeding a certain size aren't inlined
            if (body.CodeSize >= INLINE_LENGTH_LIMIT)
                return false;

            // There are other checks (..ctor, profiling, method attributes, etc.), but those aren't relevant for us

            // The method might be inlined by mono, so consider it safe to inline for the modern runtime
            return true;
        }

    }
}