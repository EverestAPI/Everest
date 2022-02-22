using Mono.Cecil;
using MonoMod.Cil;
using MonoMod.Utils;

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

    }
}
