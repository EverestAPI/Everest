using Mono.Cecil;
using MonoMod.Cil;
using MonoMod.Utils;
using System.Linq;

namespace MonoMod {
    static partial class MonoModRules {

        #region Patch-time IteratorStateMachine target utils

        /*
         * The code is referenced from part of https://github.com/icsharpcode/ILSpy/blob/84101f8/ICSharpCode.Decompiler/IL/ControlFlow/YieldReturnDecompiler.cs
         * and reimplemented to be compatible with cecil.
         *
         * Note that the implementation here is intended to just works for Celeste so lots of code are omitted, for example
         * mono compiler support.
         *
         * Feel free to submit a PR if you want to implement all those things. (and good luck implementing the expression tree parser)
         */

        /// <summary>
        /// Get MoveNext() method of the compiler-generated enumerator class of the specified method.
        /// </summary>
        public static MethodDefinition GetEnumeratorMoveNext(this MethodDefinition method) {
            if (!method.HasBody) {
                return null;
            }

            MethodDefinition enumeratorMoveNext = null;
            bool matched = false;

            new ILContext(method).Invoke(ctx => {
                ILCursor cursor = new ILCursor(ctx);

                TypeDefinition enumeratorType = null;
                MethodReference enumeratorCtor = null;

                // ldc.i4  ${initialState}
                // newobj  instance void ${enumeratorType}::.ctor(int32)
                // initial state can only be -2 (IEnumerable only, before the first call to GetEnumerator() from the creating thread)
                // or 0 (MoveNext() hasn't been called yet)
                if (!cursor.TryGotoNext(instr => instr.MatchLdcI4(out int initialState) && initialState is 0 or -2)) {
                    return;
                }
                if (!cursor.TryGotoNext(instr => instr.MatchNewobj(out enumeratorCtor) &&
                    enumeratorCtor.Parameters is {Count: 1} parameters &&
                    parameters[0].ParameterType.FullName == "System.Int32")
                ) {
                    return;
                }
                enumeratorType = enumeratorCtor.DeclaringType.SafeResolve();
                // enumerator type should be in same class as the source method and implements IEnumerator,
                // its declaring type or itself should have CompilerGenerated attribute
                if (enumeratorType == null || enumeratorType.DeclaringType != method.DeclaringType || !enumeratorType.IsCompilerGeneratedEnumerator()) {
                    return;
                }
                enumeratorMoveNext = enumeratorType.FindMethod("System.Boolean MoveNext()", simple: true);
                if (enumeratorMoveNext == null) {
                    return;
                }
                matched = true;
            });
            return matched ? enumeratorMoveNext : null;
        }

        public static bool IsCompilerGeneratedEnumerator(this TypeDefinition type) {
            if (type == null || !type.IsCompilerGeneratedOrIsInCompilerGeneratedClass() || type.DeclaringType == null) {
                return false;
            }
            foreach (InterfaceImplementation interfaceImpl in type.Interfaces) {
                TypeReference interfaceType = interfaceImpl.InterfaceType;
                if (interfaceType.FullName == "System.Collections.IEnumerator") {
                    return true;
                }
            }
            return false;
        }

        public static bool IsCompilerGeneratedOrIsInCompilerGeneratedClass(this TypeDefinition type) {
            if (type.IsCompilerGenerated()) {
                return true;
            }
            return type.DeclaringType?.IsCompilerGenerated() ?? false;
        }

        public static bool IsCompilerGenerated(this TypeDefinition type) {
            return type.HasCustomAttribute("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        }

        #endregion

        public static void SetupLegacyMonoModRelinking(MonoModder modder) {
            modder.Log($"[Celeste.Mod.mm] Relinking legacy MonoMod to glue code layer");

            // Ensure the module has a reference to Celeste.dll
            if (!(modder.Module.AssemblyReferences.FirstOrDefault(asmRef => asmRef.Name.Equals(CelesteAsmRef.Name)) is AssemblyNameReference celesteRef)) {
                modder.Module.AssemblyReferences.Add(celesteRef = CelesteAsmRef);
                modder.MapDependency(modder.Module, CelesteAsmRef);
            }

            // Add a RelinkedMonoModLegacyAttribute to the assembly
            MethodReference attrCtor = RulesModule.GetType("Celeste.Mod.Helpers.LegacyMonoMod.RelinkedMonoModLegacyAttribute").Methods.First(m => m.IsConstructor);
            attrCtor = modder.Module.ImportReference(attrCtor);
            attrCtor.DeclaringType.Scope = celesteRef; // The scope of the reference is wrong by default (it references Celeste.Mod.mm.dll instead of just Celeste.dll)
            modder.Module.Assembly.CustomAttributes.Add(new CustomAttribute(attrCtor));

            // Replace assembly references which changed
            ReplaceAssemblyRefs(modder, static asm => asm.Name.Equals("MonoMod"), GetRulesAssemblyRef("MonoMod.Patcher"));

            // Setup MonoMod.Patcher hackfixes (please just let it be rewritten already I have suffered enough ._.)
            SetupLegacyMonoModPatcherHackfixes(modder, celesteRef);

            // Convert all RelinkLegacyMonoMod attributes to MonoModLinkFrom attributes
            foreach (TypeDefinition type in RulesModule.Types)
                SetupLegacyMonoModRelinking(modder, type);
        }

        public static bool SetupLegacyMonoModRelinking(MonoModder modder, TypeDefinition type) {
            static bool SetupAttrs(MonoModder modder, ICustomAttributeProvider prov) {
                bool didRelink = false;

                foreach (CustomAttribute attr in prov.CustomAttributes)
                    if (attr.AttributeType.FullName == "MonoMod.RelinkLegacyMonoMod") {
                        // Note: usually MonoMod removes the attribute (which would be bad because the module is shared), but by calling the method directly it doesn't 
                        modder.ParseLinkFrom((MemberReference) prov, attr);
                        didRelink = true;
                    }

                return didRelink;
            }

            bool didRelink = false;
            didRelink |= SetupAttrs(modder, type);

            foreach (MethodDefinition method in type.Methods)
                didRelink |= SetupAttrs(modder, method);

            foreach (PropertyDefinition prop in type.Properties)
                didRelink |= SetupAttrs(modder, prop);

            foreach (EventDefinition evt in type.Events)
                didRelink |= SetupAttrs(modder, evt);

            foreach (FieldDefinition field in type.Fields)
                didRelink |= SetupAttrs(modder, field);

            foreach (TypeDefinition nestedType in type.NestedTypes)
                didRelink |= SetupLegacyMonoModRelinking(modder, nestedType);

            return didRelink;
        }

    }
}
