using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Core.Platforms;
using MonoMod.Utils;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using MethodImplAttributes = System.Reflection.MethodImplAttributes;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.DetourHelper")]
    public static class LegacyDetourHelper {
        public static unsafe void Write(this IntPtr to, ref int offs, byte value) {
            Unsafe.Write<byte>((void*) ((long) offs + offs), value);
            offs += 1;
            offs += 1;
        }

        public static unsafe void Write(this IntPtr to, ref int offs, ushort value) {
            Unsafe.Write<ushort>((void*) ((long) offs + offs), value);
            offs += 2;
        }

        public static unsafe void Write(this IntPtr to, ref int offs, uint value) {
            Unsafe.Write<uint>((void*) ((long) offs + offs), value);
            offs += 4;
        }

        public static unsafe void Write(this IntPtr to, ref int offs, ulong value) {
            Unsafe.Write<ulong>((void*) ((long) offs + offs), value);
            offs += 8;
        }

        public static MethodBase GetIdentifiable(this MethodBase method) => PlatformTriple.Current.GetIdentifiable(method);

        public static IntPtr GetNativeStart(this MethodBase method) => PlatformTriple.Current.GetNativeMethodBody(method);
        public static IntPtr GetNativeStart(this Delegate method) => method.Method.GetNativeStart();
        public static IntPtr GetNativeStart(this Expression method) => ((MethodCallExpression) method).Method.GetNativeStart();

        public static MethodInfo CreateILCopy(this MethodBase method) {
            using (DynamicMethodDefinition dmd = new DynamicMethodDefinition(method.GetIdentifiable()))
                return dmd.Generate();
        }
        public static bool TryCreateILCopy(this MethodBase method, out MethodInfo dm) {
            method = method.GetIdentifiable();
            if (method == null || (method.GetMethodImplementationFlags() & (MethodImplAttributes.OPTIL | MethodImplAttributes.Native | MethodImplAttributes.Runtime)) != 0) {
                dm = null;
                return false;
            }

            try {
                dm = method.CreateILCopy();
                return true;
            } catch {
                dm = null;
                return false;
            }
        }


        private struct MethodPinTracker {
            private static readonly ConcurrentDictionary<MethodBase, MethodPinTracker> _Trackers = new ConcurrentDictionary<MethodBase, MethodPinTracker>();
            public static MethodPinTracker GetTracker(MethodBase method) => _Trackers.GetOrAdd(method, static m => new MethodPinTracker(m));

            private readonly object LOCK = new object();
            public readonly MethodBase Method;
            private int pinCount;
            private IDisposable pinDisposable;

            private MethodPinTracker(MethodBase method) {
                Method = method;
                pinCount = 0;
            }

            public MethodBase Pin() {
                lock(LOCK) {
                    if (pinCount++ == 0)
                        pinDisposable = PlatformTriple.Current.Runtime.PinMethodIfNeeded(Method);
                }
                return Method;
            }

            public MethodBase Unpin() {
                lock(LOCK) {
                    if (pinCount <= 0)
                        throw new InvalidOperationException($"Method {Method} isn't pinned!");

                    if (--pinCount == 0) {
                        pinDisposable.Dispose();
                        pinDisposable = null;
                    }
                }
                return Method;
            }
        }

        public static T Pin<T>(this T method) where T : MethodBase => (T) MethodPinTracker.GetTracker(method).Pin();
        public static T Unpin<T>(this T method) where T : MethodBase => (T) MethodPinTracker.GetTracker(method).Unpin();

        public static MethodInfo GenerateNativeProxy(IntPtr target, MethodBase signature) {
            Type returnType = (signature as MethodInfo)?.ReturnType ?? typeof(void);

            ParameterInfo[] args = signature.GetParameters();
            Type[] argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                argTypes[i] = args[i].ParameterType;

            MethodInfo dm;
            using (DynamicMethodDefinition dmd = new DynamicMethodDefinition(
                $"Native<{((long) target).ToString("X16", CultureInfo.InvariantCulture)}>",
                returnType, argTypes
            ))
                dm = dmd.StubCriticalDetour().Generate().Pin();

            // Detour the new DynamicMethod into the target.
            PlatformTriple.NativeDetour detour = PlatformTriple.Current.CreateNativeDetour(dm.GetNativeStart(), target);
            GC.SuppressFinalize(detour.Simple); // Intentionally leak the hook

            return dm;
        }

        private static readonly ConstructorInfo Exception_ctor = typeof(Exception).GetConstructor(new Type[] { typeof(string) })?? throw new InvalidOperationException();
        public static DynamicMethodDefinition StubCriticalDetour(this DynamicMethodDefinition dmd) {
            ILProcessor il = dmd.GetILProcessor();
            ModuleDefinition ilModule = il.Body.Method.Module;
            for (var i = 0; i < 32; i++) {
                // Prevent mono from inlining the DynamicMethod.
                il.Emit(OpCodes.Nop);
            }
            il.Emit(OpCodes.Ldstr, $"{dmd.Definition.Name} should've been detoured!");
            il.Emit(OpCodes.Newobj, ilModule.ImportReference(Exception_ctor));
            il.Emit(OpCodes.Throw);
            return dmd;
        }

    }
}