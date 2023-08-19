using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.Utils;
using MonoMod.Cil;
using System.Collections.ObjectModel;
using System.Globalization;
using MonoMod;
using MonoMod.RuntimeDetour;
using System.Linq;
using System.Threading;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.ILHookConfig")]
    public struct LegacyILHookConfig {
        public bool ManualApply;
        public int Priority;
        public string ID;
        public IEnumerable<string> Before;
        public IEnumerable<string> After;
    }

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.ILHook")]
    public class LegacyILHook : ILegacySortableDetour {

        public static Func<LegacyILHook, MethodBase, ILContext.Manipulator, bool> OnDetour;
        public static Func<LegacyILHook, bool> OnUndo;

        // Still here in case a mod tries to access it using reflection
        private static LegacyILHookConfig ILDetourConfig = new LegacyILHookConfig() { Priority = int.MinValue / 8, Before = new string[] { "*" } };

        private static uint _GlobalIndexNext = uint.MinValue;
        internal static uint AcquireGlobalIndex() => Interlocked.Increment(ref _GlobalIndexNext)-1;

        private ILHook actualHook;

        public bool IsValid { get; private set; } = true;
        public bool IsApplied { get; private set; }

        // NOTE: This behaves slightly differently - legacy MonoMod kept non-applied hooks in the chain as well
        public int Index {
            get {
                if (actualHook?.IsApplied ?? false)
                    return -1;

                MethodDetourInfo info = DetourManager.GetDetourInfo(Method);
                using (info.WithLock())
                    return info.ILHooks.TakeWhile(h => h != actualHook.HookInfo).Count();
            }
        }

        public int MaxIndex {
            get {
                MethodDetourInfo info = DetourManager.GetDetourInfo(Method);
                using (info.WithLock())
                    return info.ILHooks.Count();
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
                    value = Manipulator.Method?.GetID(simple: true) ?? GetHashCode().ToString(CultureInfo.InvariantCulture);
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
                        _Before.AddRange(value);
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
                        _After.AddRange(value);
                    _Refresh();
                }
            }
        }

        public readonly MethodBase Method;
        public readonly ILContext.Manipulator Manipulator;

        public LegacyILHook(MethodBase from, ILContext.Manipulator manipulator, LegacyILHookConfig config) {
            from = from.GetIdentifiable();
            Method = from;
            Manipulator = manipulator;

            _GlobalIndex = AcquireGlobalIndex();
            _Priority = config.Priority;
            _ID = string.IsNullOrEmpty(config.ID) ? Manipulator.Method.GetID(simple: true) : config.ID;
            if (config.Before != null)
                _Before.AddRange(config.Before);
            if (config.After != null)
                _After.AddRange(config.After);

            if (!config.ManualApply)
                Apply();
        }

        public LegacyILHook(MethodBase from, ILContext.Manipulator manipulator, ref LegacyILHookConfig config) : this(from, manipulator, (LegacyILHookConfig) config) {}
        public LegacyILHook(MethodBase from, ILContext.Manipulator manipulator) : this(from, manipulator, LegacyDetourContext.Current?.ILHookConfig ?? default) {}

        public void Dispose() {
            if (!IsValid)
                return;

            Undo();
            Free();
        }

        public void Apply() {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyILHook));

            if (IsApplied || !(OnDetour?.InvokeWhileTrue(this, Method, Manipulator) ?? true))
                return;

            IsApplied = true;
            _Refresh();
        }

        public void Undo() {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyILHook));

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

        private void _Refresh() {
            if (!IsValid)
                return;

            actualHook?.Dispose();
            if (IsApplied) {
                actualHook = new ILHook(Method, Manipulator, LegacyDetourContext.CreateLegacyDetourConfig(ID, Priority, Before, After, GlobalIndex, true));
                GC.SuppressFinalize(actualHook);
            } else
                actualHook = null;
        }

        public MethodBase GenerateTrampoline(MethodBase signature = null) => throw new NotSupportedException();
        public T GenerateTrampoline<T>() where T : Delegate => throw new NotSupportedException();

    }
}
