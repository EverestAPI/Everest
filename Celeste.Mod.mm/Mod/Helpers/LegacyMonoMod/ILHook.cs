using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.Utils;
using MonoMod.Cil;
using System.Collections.ObjectModel;
using System.Globalization;
using MonoMod;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.ILHook")]
    public class LegacyILHook : ILegacySortableDetour {

        public static Func<LegacyILHook, MethodBase, ILContext.Manipulator, bool> OnDetour;
        public static Func<LegacyILHook, bool> OnUndo;

        // Still here in case any external mod tries to access it using reflection
        private static LegacyDetourConfig ILDetourConfig = new LegacyDetourConfig(null, priority: int.MinValue / 8, before: new string[] { "*" });
        private static uint _GlobalIndexNext = uint.MinValue;

        private ILHook actualHook;

        public bool IsValid { get; private set; } = true;
        public bool IsApplied { get; private set; }

        public int Index => throw new NotSupportedException("ILHook.Index is no longer supported");
        public int MaxIndex => throw new NotSupportedException("ILHook.MaxIndex is no longer supported");

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

        public LegacyILHook(MethodBase from, ILContext.Manipulator manipulator, LegacyDetourConfig config) {
            from = from.GetIdentifiable();
            Method = from;
            Manipulator = manipulator;

            _GlobalIndex = _GlobalIndexNext++;

            _Priority = config.Priority;
            _ID = config.Id;
            if (config.Before != null)
                _Before.AddRange(config.Before);
            if (config.After != null)
                _After.AddRange(config.After);

            if (!config.ManualApply)
                Apply();
        }

        public LegacyILHook(MethodBase from, ILContext.Manipulator manipulator, ref LegacyDetourConfig config) : this(from, manipulator, (LegacyDetourConfig) config) {}
        public LegacyILHook(MethodBase from, ILContext.Manipulator manipulator) : this(from, manipulator, (DetourContext.CurrentConfig as LegacyDetourConfig) ?? new LegacyDetourConfig()) {}

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
                actualHook = new ILHook(Method, Manipulator, new DetourConfig(ID, Priority, Before, After));
                GC.SuppressFinalize(actualHook);
            } else
                actualHook = null;
        }

        public MethodBase GenerateTrampoline(MethodBase signature = null) => throw new NotSupportedException();
        public T GenerateTrampoline<T>() where T : Delegate => throw new NotSupportedException();

    }
}
