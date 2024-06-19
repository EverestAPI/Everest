using System;
using System.Reflection;
using MonoMod.Utils;
using System.Collections.Generic;
using MonoMod;
using MonoMod.Core.Platforms;
using MonoMod.Core;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.NativeDetourConfig")]
    public struct LegacyNativeDetourConfig {
        public bool ManualApply, SkipILCopy;
    }

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.NativeDetour")]
    public class LegacyNativeDetour : ILegacyDetour {

        public static Func<LegacyNativeDetour, MethodBase, IntPtr, IntPtr, bool> OnDetour;
        public static Func<LegacyNativeDetour, bool> OnUndo;
        public static Func<LegacyNativeDetour, MethodBase, MethodBase> OnGenerateTrampoline;

        private IntPtr _From, _To;
        private ICoreDetourBase actualDetour;

        public bool IsValid { get; private set; }
        public bool IsApplied { get; private set; }

        public LegacyNativeDetourData Data => new LegacyNativeDetourData() { Method = _From, Target = _To };
        public readonly MethodBase Method;

        private readonly MethodBase _FromMethod, _ToMethod;

        private readonly MethodInfo _BackupMethod;

        [MonoModLinkFrom("System.IntPtr Celeste.Mod.Helpers.LegacyMonoMod.LegacyNativeDetour::_BackupNative")]
        private IntPtr _BackupNative => throw new NotSupportedException("NativeDetour._BackupNative is no longer supported");

        private HashSet<MethodBase> _Pinned = new HashSet<MethodBase>();

        private LegacyNativeDetour(MethodBase fromMethod, MethodBase toMethod, IntPtr from, IntPtr to, ref LegacyNativeDetourConfig config) {
            if (from == to)
                throw new InvalidOperationException($"Cannot detour from a location to itself! (from: {from:X16} to: {to:X16} method: {from})");

            _FromMethod = fromMethod;
            _ToMethod = toMethod;

            fromMethod = fromMethod?.GetIdentifiable();
            toMethod = toMethod?.GetIdentifiable();
            Method = fromMethod;

            if (!(OnDetour?.InvokeWhileTrue(this, fromMethod, from, to) ?? true))
                return;
            IsValid = true;

            if (!config.SkipILCopy)
                fromMethod?.TryCreateILCopy(out _BackupMethod);

            if (!config.ManualApply)
                Apply();
        }

        public LegacyNativeDetour(MethodBase method, IntPtr from, IntPtr to, ref LegacyNativeDetourConfig config) : this(method, null, from, to, ref config) {}
        public LegacyNativeDetour(MethodBase method, IntPtr from, IntPtr to, LegacyNativeDetourConfig config) : this(method, from, to, ref config) {}
        public LegacyNativeDetour(MethodBase method, IntPtr from, IntPtr to) : this(method, from, to, default) {}
        public LegacyNativeDetour(IntPtr from, IntPtr to, ref LegacyNativeDetourConfig config) : this(null, from, to, ref config) {}
        public LegacyNativeDetour(IntPtr from, IntPtr to, LegacyNativeDetourConfig config) : this(null, from, to, ref config) {}
        public LegacyNativeDetour(IntPtr from, IntPtr to) : this(null, from, to) {}

        public LegacyNativeDetour(MethodBase from, IntPtr to, ref LegacyNativeDetourConfig config)
            => throw new NotSupportedException("This *evil* constructor is no longer supported");
        public LegacyNativeDetour(MethodBase from, IntPtr to, LegacyNativeDetourConfig config)
            => throw new NotSupportedException("This *evil* constructor is no longer supported");
        public LegacyNativeDetour(MethodBase from, IntPtr to)
            => throw new NotSupportedException("This *evil* constructor is no longer supported");
        public LegacyNativeDetour(IntPtr from, MethodBase to, ref LegacyNativeDetourConfig config)
            => throw new NotSupportedException("This *evil* constructor is no longer supported");
        public LegacyNativeDetour(IntPtr from, MethodBase to, LegacyNativeDetourConfig config)
            => throw new NotSupportedException("This *evil* constructor is no longer supported");
        public LegacyNativeDetour(IntPtr from, MethodBase to)
            => throw new NotSupportedException("This *evil* constructor is no longer supported");

        public LegacyNativeDetour(MethodBase from, MethodBase to, ref LegacyNativeDetourConfig config)
            : this(from, PlatformTriple.Current.GetRealDetourTarget(from, to), from.Pin().GetNativeStart(), PlatformTriple.Current.GetRealDetourTarget(from, to).GetNativeStart(), ref config) {
            _Pinned.Add(from);
            _Pinned.Add(to);
        }
        public LegacyNativeDetour(MethodBase from, MethodBase to, LegacyNativeDetourConfig config) : this(from, to, ref config) {}
        public LegacyNativeDetour(MethodBase from, MethodBase to) : this(from, to, default) {}

        public LegacyNativeDetour(Delegate from, IntPtr to, ref LegacyNativeDetourConfig config) : this(from.Method, to, ref config) {}
        public LegacyNativeDetour(Delegate from, IntPtr to, LegacyNativeDetourConfig config) : this(from.Method, to, ref config) {}
        public LegacyNativeDetour(Delegate from, IntPtr to) : this(from.Method, to) {}

        public LegacyNativeDetour(IntPtr from, Delegate to, ref LegacyNativeDetourConfig config) : this(from, to.Method, ref config) {}
        public LegacyNativeDetour(IntPtr from, Delegate to, LegacyNativeDetourConfig config) : this(from, to.Method, ref config) {}
        public LegacyNativeDetour(IntPtr from, Delegate to) : this(from, to.Method) {}

        public LegacyNativeDetour(Delegate from, Delegate to, ref LegacyNativeDetourConfig config) : this(from.Method, to.Method, ref config) {}
        public LegacyNativeDetour(Delegate from, Delegate to, LegacyNativeDetourConfig config) : this(from.Method, to.Method, ref config) {}
        public LegacyNativeDetour(Delegate from, Delegate to) : this(from.Method, to.Method) {}

        public void Dispose() {
            if (!IsValid)
                return;

            Undo();
            Free();
        }

        public void Apply() {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyNativeDetour));

            if (IsApplied)
                return;
            IsApplied = true;

            if (_FromMethod != null && _ToMethod != null)
                // Make this slightly less evil ._.
                actualDetour = DetourFactory.Current.CreateDetour(_FromMethod, _ToMethod);
            else 
                actualDetour = DetourFactory.Current.CreateNativeDetour(_From, _To);

            GC.SuppressFinalize(actualDetour);
        }

        public void Undo() {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyNativeDetour));

            if (!(OnUndo?.InvokeWhileTrue(this) ?? true))
                return;

            if (!IsApplied)
                return;
            IsApplied = false;

            actualDetour?.Dispose();
            actualDetour = null;
        }

        public void ChangeSource(IntPtr newSource) {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyNativeDetour));

            _From = newSource;
            if (IsApplied)
                Undo();
            Apply();
        }

        public void ChangeTarget(IntPtr newTarget) {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(LegacyNativeDetour));

            _To = newTarget;
            if (IsApplied)
                Undo();
            Apply();
        }

        public void Free() {
            if (!IsValid)
                return;
            IsValid = false;

            if (!IsApplied) {
                foreach (MethodBase method in _Pinned)
                    method.Unpin();
                _Pinned.Clear();
            }
        }

        // I'm not reimplementing all this jank ._.
        public MethodBase GenerateTrampoline(MethodBase signature = null) => throw new NotSupportedException("NativeDetour.GenerateTrampoline is no longer supported");

        public T GenerateTrampoline<T>() where T : Delegate {
            if (!typeof(Delegate).IsAssignableFrom(typeof(T)))
                throw new InvalidOperationException($"Type {typeof(T)} not a delegate type.");

            return GenerateTrampoline(typeof(T).GetMethod("Invoke")).CreateDelegate(typeof(T)) as T;
        }

    }

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.NativeDetourData")]
    public struct LegacyNativeDetourData {

        public IntPtr Method, Target;


        [MonoModLinkFrom("System.Byte Celeste.Mod.Helpers.LegacyMonoMod.LegacyNativeDetourData::Type")]
        public byte Type => throw new NotSupportedException("NativeDetourData.Type is no longer supported");

        [MonoModLinkFrom("System.UInt32 Celeste.Mod.Helpers.LegacyMonoMod.LegacyNativeDetourData::Size")]
        public uint Size => throw new NotSupportedException("NativeDetourData.Size is no longer supported");

        [MonoModLinkFrom("System.IntPtr Celeste.Mod.Helpers.LegacyMonoMod.LegacyNativeDetourData::Extra")]
        public IntPtr Extra => throw new NotSupportedException("NativeDetourData.Extra is no longer supported");

    }
}
