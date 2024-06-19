using System;
using System.Reflection;
using System.Linq.Expressions;
using MonoMod.Utils;
using MonoMod;
using Mono.Cecil.Cil;
using System.Collections.Generic;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    // Note that we could use the new Hook class directly, but we don't actually have to
    // So just remain as true to the original as possible

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.HookConfig")]
    public struct LegacyHookConfig {
        public bool ManualApply;
        public int Priority;
        public string ID;
        public IEnumerable<string> Before, After;
    }

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.Hook")]
    public class LegacyHook : ILegacyDetour {

        public static Func<LegacyHook, MethodBase, MethodBase, object, bool> OnDetour;
        public static Func<LegacyHook, bool> OnUndo;
        public static Func<LegacyHook, MethodBase, MethodBase> OnGenerateTrampoline;

        public bool IsValid => _Detour.IsValid;
        public bool IsApplied => _Detour.IsApplied;

        public readonly MethodBase Method;
        public readonly MethodBase Target;
        public readonly MethodBase TargetReal;

        public readonly object DelegateTarget;

        private LegacyDetour _Detour;
        public LegacyDetour Detour => _Detour;

        private readonly Type _OrigDelegateType;
        private readonly MethodInfo _OrigDelegateInvoke;

        private DataScope<DynamicReferenceCell>? _RefTargetCell, _RefTrampolineCell, _RefTrampolineTmpCell;

        [MonoModLinkFrom("System.Nullable`1<System.Int32> Celeste.Mod.Helpers.LegacyMonoMod.LegacyHook::_RefTarget")]
        private int? _RefTarget => _RefTargetCell?.Data.Index;
        [MonoModLinkFrom("System.Nullable`1<System.Int32> Celeste.Mod.Helpers.LegacyMonoMod.LegacyHook::_RefTrampoline")]
        private int? _RefTrampoline => _RefTrampolineCell?.Data.Index;
        [MonoModLinkFrom("System.Nullable`1<System.Int32> Celeste.Mod.Helpers.LegacyMonoMod.LegacyHook::_RefTrampolineTmp")]
        private int? _RefTrampolineTmp => _RefTrampolineTmpCell?.Data.Index;

        public LegacyHook(MethodBase from, MethodInfo to, object target, ref LegacyHookConfig config) {
            from = from.GetIdentifiable();
            Method = from;
            Target = to;
            DelegateTarget = target;

            // Check if LegacyHook ret -> method ret is valid. Don't check for method ret -> LegacyHook ret, as that's too strict.
            Type returnType = (from as MethodInfo)?.ReturnType ?? typeof(void);
            if (to.ReturnType != returnType && !to.ReturnType.IsCompatible(returnType))
                throw new InvalidOperationException($"Return type of LegacyHook for {from} doesn't match, must be {((from as MethodInfo)?.ReturnType ?? typeof(void)).FullName}");

            if (target == null && !to.IsStatic) {
                throw new InvalidOperationException($"LegacyHook for method {from} must be static, or you must pass a target instance.");
            }

            ParameterInfo[] LegacyHookArgs = Target.GetParameters();

            // Check if the parameters match.
            // If the delegate has got an extra first parameter that itself is a delegate, it's the orig trampoline passthrough.
            ParameterInfo[] args = Method.GetParameters();
            Type[] argTypes;
            if (!Method.IsStatic) {
                argTypes = new Type[args.Length + 1];
                argTypes[0] = Method.GetThisParamType();
                for (int i = 0; i < args.Length; i++)
                    argTypes[i + 1] = args[i].ParameterType;
            } else {
                argTypes = new Type[args.Length];
                for (int i = 0; i < args.Length; i++)
                    argTypes[i] = args[i].ParameterType;
            }

            Type origType = null;
            if (LegacyHookArgs.Length == argTypes.Length + 1 && typeof(Delegate).IsAssignableFrom(LegacyHookArgs[0].ParameterType))
                _OrigDelegateType = origType = LegacyHookArgs[0].ParameterType;
            else if (LegacyHookArgs.Length != argTypes.Length)
                throw new InvalidOperationException($"Parameter count of LegacyHook for {from} doesn't match, must be {argTypes.Length}");

            for (int i = 0; i < argTypes.Length; i++) {
                Type argMethod = argTypes[i];
                Type argHook = LegacyHookArgs[i + (origType == null ? 0 : 1)].ParameterType;
                if (!argMethod.IsCompatible(argHook))
                    throw new InvalidOperationException($"Parameter #{i} of LegacyHook for {from} doesn't match, must be {argMethod.FullName} or related");
            }

            MethodInfo origInvoke = _OrigDelegateInvoke = origType?.GetMethod("Invoke");

            DynamicMethodDefinition dmd;
            ILProcessor il;

            using (dmd = new DynamicMethodDefinition(
                $"Hook<{Method.GetID(simple: true)}>?{GetHashCode()}",
                (Method as MethodInfo)?.ReturnType ?? typeof(void), argTypes
            )) {
                il = dmd.GetILProcessor();

                if (target != null)
                    _RefTargetCell = il.EmitNewTypedReference(target, out _);

                if (origType != null)
                    _RefTrampolineCell = il.EmitNewTypedReference<Delegate>(null, out _);

                for (int i = 0; i < argTypes.Length; i++)
                    il.Emit(OpCodes.Ldarg, i);

                il.Emit(OpCodes.Call, Target);

                il.Emit(OpCodes.Ret);

                TargetReal = dmd.Generate().Pin();
            }

            // Temporarily provide a trampoline that waits for the proper trampoline.
            if (origType != null) {
                ParameterInfo[] origArgs = origInvoke.GetParameters();
                Type[] origArgTypes = new Type[origArgs.Length];
                for (int i = 0; i < origArgs.Length; i++)
                    origArgTypes[i] = origArgs[i].ParameterType;

                using (dmd = new DynamicMethodDefinition(
                    $"Chain:TMP<{Method.GetID(simple: true)}>?{GetHashCode()}",
                    (origInvoke as MethodInfo)?.ReturnType ?? typeof(void), origArgTypes
                )) {
                    il = dmd.GetILProcessor();

                    // while (ref == null) { }
                    _RefTrampolineTmpCell = il.EmitNewTypedReference<Delegate>(null, out _);
                    il.Emit(OpCodes.Brfalse, il.Body.Instructions[0]);

                    // Invoke the generated delegate.
                    il.EmitLoadTypedReference(_RefTrampolineTmpCell.Value.Data, typeof(Delegate));

                    for (int i = 0; i < argTypes.Length; i++)
                        il.Emit(OpCodes.Ldarg, i);

                    il.Emit(OpCodes.Callvirt, origInvoke);

                    il.Emit(OpCodes.Ret);

                    DynamicReferenceManager.SetValue(_RefTrampolineCell.Value.Data, dmd.Generate().CreateDelegate(origType));
                }
            }

            _Detour = new LegacyDetour(Method, TargetReal, new LegacyDetourConfig() {
                ManualApply = true,
                Priority = config.Priority,
                ID = config.ID,
                Before = config.Before,
                After = config.After
            });

            _UpdateOrig(null);

            if (!config.ManualApply)
                Apply();
        }

        public LegacyHook(MethodBase from, MethodInfo to, object target, LegacyHookConfig config) : this(from, to, target, ref config) {}
        public LegacyHook(MethodBase from, MethodInfo to, object target) : this(from, to, target, LegacyDetourContext.Current?.HookConfig ?? default) {}
        public LegacyHook(MethodBase from, MethodInfo to, ref LegacyHookConfig config) : this(from, to, null, ref config) {}
        public LegacyHook(MethodBase from, MethodInfo to, LegacyHookConfig config) : this(from, to, null, ref config) {}
        public LegacyHook(MethodBase from, MethodInfo to) : this(from, to, null) {}
        public LegacyHook(MethodBase method, IntPtr to, ref LegacyHookConfig config) : this(method, LegacyDetourHelper.GenerateNativeProxy(to, method), null, ref config) {}
        public LegacyHook(MethodBase method, IntPtr to, LegacyHookConfig config) : this(method, LegacyDetourHelper.GenerateNativeProxy(to, method), null, ref config) {}
        public LegacyHook(MethodBase method, IntPtr to) : this(method, LegacyDetourHelper.GenerateNativeProxy(to, method), null) {}
        public LegacyHook(MethodBase method, Delegate to, ref LegacyHookConfig config) : this(method, to.Method, to.Target, ref config) {}
        public LegacyHook(MethodBase method, Delegate to, LegacyHookConfig config) : this(method, to.Method, to.Target, ref config) {}
        public LegacyHook(MethodBase method, Delegate to) : this(method, to.Method, to.Target) {}
        public LegacyHook(Delegate from, IntPtr to, ref LegacyHookConfig config) : this(from.Method, to, ref config) {}
        public LegacyHook(Delegate from, IntPtr to, LegacyHookConfig config) : this(from.Method, to, ref config) {}
        public LegacyHook(Delegate from, IntPtr to) : this(from.Method, to) {}
        public LegacyHook(Delegate from, Delegate to, ref LegacyHookConfig config) : this(from.Method, to, ref config) {}
        public LegacyHook(Delegate from, Delegate to, LegacyHookConfig config) : this(from.Method, to, ref config) {}
        public LegacyHook(Delegate from, Delegate to) : this(from.Method, to) {}
        public LegacyHook(Expression from, IntPtr to, ref LegacyHookConfig config) : this(((MethodCallExpression) from).Method, to, ref config) {}
        public LegacyHook(Expression from, IntPtr to, LegacyHookConfig config) : this(((MethodCallExpression) from).Method, to, ref config) {}
        public LegacyHook(Expression from, IntPtr to) : this(((MethodCallExpression) from).Method, to) {}
        public LegacyHook(Expression from, Delegate to, ref LegacyHookConfig config) : this(((MethodCallExpression) from).Method, to, ref config) {}
        public LegacyHook(Expression from, Delegate to, LegacyHookConfig config) : this(((MethodCallExpression) from).Method, to, ref config) {}
        public LegacyHook(Expression from, Delegate to) : this(((MethodCallExpression) from).Method, to) {}
        public LegacyHook(Expression<Action> from, IntPtr to, ref LegacyHookConfig config) : this(from.Body, to, ref config) {}
        public LegacyHook(Expression<Action> from, IntPtr to, LegacyHookConfig config) : this(from.Body, to, ref config) {}
        public LegacyHook(Expression<Action> from, IntPtr to) : this(from.Body, to) {}
        public LegacyHook(Expression<Action> from, Delegate to, ref LegacyHookConfig config) : this(from.Body, to, ref config) {}
        public LegacyHook(Expression<Action> from, Delegate to, LegacyHookConfig config) : this(from.Body, to, ref config) {}
        public LegacyHook(Expression<Action> from, Delegate to) : this(from.Body, to) {}

        public void Dispose() {
            if (!IsValid)
                return;

            Undo();
            Free();
        }

        public void Apply() {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyHook));

            if (!IsApplied && !(OnDetour?.InvokeWhileTrue(this, Method, Target, DelegateTarget) ?? true))
                return;

            _Detour.Apply();
        }

        public void Undo() {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyHook));

            if (IsApplied && !(OnUndo?.InvokeWhileTrue(this) ?? true))
                return;

            _Detour.Undo();
            if (!IsValid)
                _Free();
        }

        public void Free() {
            if (!IsValid)
                return;

            _Detour.Free();
            _Free();
        }

        private void _Free() {
            _RefTargetCell?.Dispose();
            _RefTrampolineCell?.Dispose();
            _RefTrampolineTmpCell?.Dispose();
        }

        public MethodBase GenerateTrampoline(MethodBase signature = null) {
            MethodBase remoteTrampoline = OnGenerateTrampoline?.InvokeWhileNull<MethodBase>(this, signature);
            if (remoteTrampoline != null)
                return remoteTrampoline;

            return _Detour.GenerateTrampoline(signature);
        }

        public T GenerateTrampoline<T>() where T : Delegate {
            if (!typeof(Delegate).IsAssignableFrom(typeof(T)))
                throw new InvalidOperationException($"Type {typeof(T)} not a delegate type.");

            return GenerateTrampoline(typeof(T).GetMethod("Invoke")).CreateDelegate(typeof(T)) as T;
        }

        internal void _UpdateOrig(MethodBase invoke) {
            if (_OrigDelegateType == null)
                return;

            Delegate orig = (invoke ?? GenerateTrampoline(_OrigDelegateInvoke)).CreateDelegate(_OrigDelegateType);
            DynamicReferenceManager.SetValue(_RefTrampolineCell.Value.Data, orig);
            DynamicReferenceManager.SetValue(_RefTrampolineTmpCell.Value.Data, orig);
        }
    }

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.Hook`1")]
    public class LegacyHook<T> : LegacyHook {
        public LegacyHook(Expression<Action> from, T to, ref LegacyHookConfig config) : base(from.Body, to as Delegate, ref config) {}
        public LegacyHook(Expression<Action> from, T to, LegacyHookConfig config) : base(from.Body, to as Delegate, ref config) {}
        public LegacyHook(Expression<Action> from, T to) : base(from.Body, to as Delegate) {}
        public LegacyHook(Expression<Func<T>> from, IntPtr to, ref LegacyHookConfig config) : base(from.Body, to, ref config) {}
        public LegacyHook(Expression<Func<T>> from, IntPtr to, LegacyHookConfig config) : base(from.Body, to, ref config) {}
        public LegacyHook(Expression<Func<T>> from, IntPtr to) : base(from.Body, to) {}
        public LegacyHook(Expression<Func<T>> from, Delegate to, ref LegacyHookConfig config) : base(from.Body, to, ref config) {}
        public LegacyHook(Expression<Func<T>> from, Delegate to, LegacyHookConfig config) : base(from.Body, to, ref config) {}
        public LegacyHook(Expression<Func<T>> from, Delegate to) : base(from.Body, to) {}
        public LegacyHook(T from, IntPtr to, ref LegacyHookConfig config) : base(from as Delegate, to, ref config) {}
        public LegacyHook(T from, IntPtr to, LegacyHookConfig config) : base(from as Delegate, to, ref config) {}
        public LegacyHook(T from, IntPtr to) : base(from as Delegate, to) {}
        public LegacyHook(T from, T to, ref LegacyHookConfig config) : base(from as Delegate, to as Delegate, ref config) {}
        public LegacyHook(T from, T to, LegacyHookConfig config) : base(from as Delegate, to as Delegate, ref config) {}
        public LegacyHook(T from, T to) : base(from as Delegate, to as Delegate) {}
    }

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.Hook`2")]
    public class LegacyHook<TFrom, TTo> : LegacyHook {
        public LegacyHook(Expression<Func<TFrom>> from, TTo to, ref LegacyHookConfig config) : base(from.Body, to as Delegate) {}
        public LegacyHook(Expression<Func<TFrom>> from, TTo to, LegacyHookConfig config) : base(from.Body, to as Delegate) {}
        public LegacyHook(Expression<Func<TFrom>> from, TTo to) : base(from.Body, to as Delegate) {}
        public LegacyHook(TFrom from, TTo to, ref LegacyHookConfig config) : base(from as Delegate, to as Delegate) {}
        public LegacyHook(TFrom from, TTo to, LegacyHookConfig config) : base(from as Delegate, to as Delegate) {}
        public LegacyHook(TFrom from, TTo to) : base(from as Delegate, to as Delegate) {}
    }
}
