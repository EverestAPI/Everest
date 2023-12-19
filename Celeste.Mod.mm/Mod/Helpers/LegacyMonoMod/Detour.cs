using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;
using MonoMod.Utils;
using Mono.Cecil.Cil;
using System.Collections.ObjectModel;
using MonoMod;
using MonoMod.RuntimeDetour;
using MonoMod.Core.Platforms;
using System.Linq;
using MonoMod.Core;
using System.Threading;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.DetourConfig")]
    public struct LegacyDetourConfig {
        public bool ManualApply;
        public int Priority;
        public string ID;
        public IEnumerable<string> Before, After;
    }

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.Detour")]
    public class LegacyDetour : ILegacySortableDetour {

        private static uint _GlobalIndexNext = uint.MinValue;
        internal static uint AcquireGlobalIndex() => Interlocked.Increment(ref _GlobalIndexNext)-1;

        public static Func<LegacyDetour, MethodBase, MethodBase, bool> OnDetour;
        public static Func<LegacyDetour, bool> OnUndo;
        public static Func<LegacyDetour, MethodBase, MethodBase> OnGenerateTrampoline;

        private Hook actualHook;

        public bool IsValid { get; private set; } = true;
        public bool IsApplied { get; private set; }
        private bool IsTop {
            get {
                MethodDetourInfo info = DetourManager.GetDetourInfo(Method);
                using (info.WithLock())
                    return info.FirstDetour == actualHook?.DetourInfo;
            }
        }

        // NOTE: This behaves slightly differently - legacy MonoMod kept non-applied detours in the chain as well
        public int Index {
            get {
                if (actualHook?.IsApplied ?? false)
                    return -1;

                MethodDetourInfo info = DetourManager.GetDetourInfo(Method);
                using (info.WithLock())
                    return info.Detours.TakeWhile(d => d != actualHook.DetourInfo).Count();
            }
        }

        public int MaxIndex {
            get {
                MethodDetourInfo info = DetourManager.GetDetourInfo(Method);
                using (info.WithLock())
                    return info.Detours.Count();
            }
        }

        private readonly uint _GlobalIndex;
        public uint GlobalIndex => _GlobalIndex;

        private int _Priority;
        public int Priority {
            get => _Priority;
            set {
                if (_Priority == value)
                    return;
                _Priority = value;
                _Refresh();
            }
        }

        private string _ID;
        public string ID {
            get => _ID;
            set {
                if (string.IsNullOrEmpty(value))
                    value = Target.GetID(simple: true);
                if (_ID == value)
                    return;
                _ID = value;
                _Refresh();
            }
        }

        private List<string> _Before = new List<string>();
        private ReadOnlyCollection<string> _BeforeRO;
        public IEnumerable<string> Before {
            get => _BeforeRO ?? (_BeforeRO = _Before.AsReadOnly());
            set {
                lock (_Before) {
                    _Before.Clear();
                    if (value != null)
                        foreach (string id in value)
                            _Before.Add(id);
                    _Refresh();
                }
            }
        }

        private List<string> _After = new List<string>();
        private ReadOnlyCollection<string> _AfterRO;

        public IEnumerable<string> After {
            get => _AfterRO ?? (_AfterRO = _After.AsReadOnly());
            set {
                lock (_After) {
                    _After.Clear();
                    if (value != null)
                        foreach (string id in value)
                            _After.Add(id);
                    _Refresh();
                }
            }
        }

        public readonly MethodBase Method;
        public readonly MethodBase Target;
        public readonly MethodBase TargetReal;

        // We have to maintain our own trampoline as we'll dispose the actual hooks when not applied
        private MethodInfo _ChainedTrampoline;
        private ICoreDetour _ChainedTrampolineDetour;

        public LegacyDetour(MethodBase from, MethodBase to, LegacyDetourConfig config) {
            from = from.GetIdentifiable();
            if (from.Equals(to))
                throw new ArgumentException("Cannot LegacyDetour a method to itself!");

            Method = from;
            Target = to;
            TargetReal = PlatformTriple.Current.GetRealDetourTarget(from, to);

            _GlobalIndex = AcquireGlobalIndex();
            _Priority = config.Priority;
            _ID = string.IsNullOrEmpty(config.ID) ? Target.GetID(simple: true) : config.ID;

            if (config.Before != null)
                _Before.AddRange(config.Before);
            if (config.After != null)
                _After.AddRange(config.After);

            // Generate a "chained trampoline" DynamicMethod.
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

            using (DynamicMethodDefinition dmd = new DynamicMethodDefinition($"LegacyChain<{Method.GetID(simple: true)}>?{GetHashCode()}", (Method as MethodInfo)?.ReturnType ?? typeof(void), argTypes))
                _ChainedTrampoline = dmd.StubCriticalDetour().Generate().Pin();

            if (!config.ManualApply)
                Apply();
        }

        public LegacyDetour(MethodBase from, MethodBase to, ref LegacyDetourConfig config) : this(from, to, config) {}
        public LegacyDetour(MethodBase from, MethodBase to) : this(from, to, LegacyDetourContext.Current?.DetourConfig ?? default) {}
        public LegacyDetour(MethodBase method, IntPtr to, ref LegacyDetourConfig config) : this(method, LegacyDetourHelper.GenerateNativeProxy(to, method), ref config) {}
        public LegacyDetour(MethodBase method, IntPtr to, LegacyDetourConfig config) : this(method, LegacyDetourHelper.GenerateNativeProxy(to, method), ref config) {}
        public LegacyDetour(MethodBase method, IntPtr to) : this(method, LegacyDetourHelper.GenerateNativeProxy(to, method)) {}
        public LegacyDetour(Delegate from, IntPtr to, ref LegacyDetourConfig config) : this(from.Method, to, ref config) {}
        public LegacyDetour(Delegate from, IntPtr to, LegacyDetourConfig config) : this(from.Method, to, ref config) {}
        public LegacyDetour(Delegate from, IntPtr to) : this(from.Method, to) {}
        public LegacyDetour(Delegate from, Delegate to, ref LegacyDetourConfig config) : this(from.Method, to.Method, ref config) {}
        public LegacyDetour(Delegate from, Delegate to, LegacyDetourConfig config) : this(from.Method, to.Method, ref config) {}
        public LegacyDetour(Delegate from, Delegate to) : this(from.Method, to.Method) {}
        public LegacyDetour(Expression from, IntPtr to, ref LegacyDetourConfig config) : this(((MethodCallExpression) from).Method, to, ref config) {}
        public LegacyDetour(Expression from, IntPtr to, LegacyDetourConfig config) : this(((MethodCallExpression) from).Method, to, ref config) {}
        public LegacyDetour(Expression from, IntPtr to) : this(((MethodCallExpression) from).Method, to) {}
        public LegacyDetour(Expression from, Expression to, ref LegacyDetourConfig config) : this(((MethodCallExpression) from).Method, ((MethodCallExpression) to).Method, ref config) {}
        public LegacyDetour(Expression from, Expression to, LegacyDetourConfig config) : this(((MethodCallExpression) from).Method, ((MethodCallExpression) to).Method, ref config) {}
        public LegacyDetour(Expression from, Expression to) : this(((MethodCallExpression) from).Method, ((MethodCallExpression) to).Method) {}
        public LegacyDetour(Expression<Action> from, IntPtr to, ref LegacyDetourConfig config) : this(from.Body, to, ref config) {}
        public LegacyDetour(Expression<Action> from, IntPtr to, LegacyDetourConfig config) : this(from.Body, to, ref config) {}
        public LegacyDetour(Expression<Action> from, IntPtr to) : this(from.Body, to) {}
        public LegacyDetour(Expression<Action> from, Expression<Action> to, ref LegacyDetourConfig config) : this(from.Body, to.Body, ref config) {}
        public LegacyDetour(Expression<Action> from, Expression<Action> to, LegacyDetourConfig config) : this(from.Body, to.Body, ref config) {}
        public LegacyDetour(Expression<Action> from, Expression<Action> to) : this(from.Body, to.Body) {}

        public void Dispose() {
            if (!IsValid)
                return;

            Undo();
            Free();
        }

        public void Apply() {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyDetour));

            if (IsApplied || !(OnDetour?.InvokeWhileTrue(this, Method, Target) ?? true))
                return;

            IsApplied = true;
            _Refresh();
        }

        public void Undo() {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyDetour));

            if (!IsApplied || !(OnUndo?.InvokeWhileTrue(this) ?? true))
                return;

            IsApplied = false;
            _Refresh();
        }

        public void Free() {
            if (!IsValid)
                return;

            Undo();
            IsValid = false;
        }

        private static readonly PropertyInfo IDetour_NextTrampoline
            = typeof(Hook).Assembly.GetType("MonoMod.RuntimeDetour.IDetour").GetProperty("NextTrampoline", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Couldn't get IDetour.NextTrampoline property");

        private static readonly PropertyInfo IDetourTrampoline_TrampolineMethod
            = typeof(Hook).Assembly.GetType("MonoMod.RuntimeDetour.IDetourTrampoline").GetProperty("TrampolineMethod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Couldn't get IDetourTrampoline.TrampolineMethod property");

        private void _Refresh() {
            if (!IsValid)
                return;

            actualHook?.Dispose();
            if (IsApplied) {
                actualHook = new Hook(Method, (MethodInfo) Target, LegacyDetourContext.CreateLegacyDetourConfig(ID, Priority, Before, After, GlobalIndex));
                GC.SuppressFinalize(actualHook);

                // Update the trampoline detour
                _ChainedTrampolineDetour?.Dispose();
                _ChainedTrampolineDetour = DetourFactory.Current.CreateDetour(_ChainedTrampoline, (MethodBase) IDetourTrampoline_TrampolineMethod.GetValue(IDetour_NextTrampoline.GetValue(actualHook)));
                GC.SuppressFinalize(_ChainedTrampolineDetour);
            } else
                actualHook = null;
        }

        public MethodBase GenerateTrampoline(MethodBase signature = null) {
            MethodBase remoteTrampoline = OnGenerateTrampoline?.InvokeWhileNull<MethodBase>(this, signature);
            if (remoteTrampoline != null)
                return remoteTrampoline;

            if (signature == null)
                signature = Target;

            Type returnType = (signature as MethodInfo)?.ReturnType ?? typeof(void);

            ParameterInfo[] args = signature.GetParameters();
            Type[] argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                argTypes[i] = args[i].ParameterType;

            using (DynamicMethodDefinition dmd = new DynamicMethodDefinition(
                $"LegacyTrampoline<{Method.GetID(simple: true)}>?{GetHashCode()}",
                returnType, argTypes
            )) {
                ILProcessor il = dmd.GetILProcessor();

                for (int i = 0; i < 32; i++) {
                    // Prevent mono from inlining the DynamicMethod.
                    il.Emit(OpCodes.Nop);
                }

                for (int i = 0; i < argTypes.Length; i++)
                    il.Emit(OpCodes.Ldarg, i);
                il.Emit(OpCodes.Call, _ChainedTrampoline);
                il.Emit(OpCodes.Ret);

                return dmd.Generate();
            }
        }

        public T GenerateTrampoline<T>() where T : Delegate {
            if (!typeof(Delegate).IsAssignableFrom(typeof(T)))
                throw new InvalidOperationException($"Type {typeof(T)} not a delegate type.");

            return GenerateTrampoline(typeof(T).GetMethod("Invoke")).CreateDelegate(typeof(T)) as T;
        }
    }

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.Detour`1")]
    public class LegacyDetour<T> : LegacyDetour where T : Delegate {
        public LegacyDetour(T from, IntPtr to, ref LegacyDetourConfig config) : base(from, to, ref config) {}
        public LegacyDetour(T from, IntPtr to, LegacyDetourConfig config) : base(from, to, ref config) {}
        public LegacyDetour(T from, IntPtr to) : base(from, to) {}
        public LegacyDetour(T from, T to, ref LegacyDetourConfig config) : base(from, to, ref config) {}
        public LegacyDetour(T from, T to, LegacyDetourConfig config) : base(from, to, ref config) {}
        public LegacyDetour(T from, T to) : base(from, to) {}
    }
}
