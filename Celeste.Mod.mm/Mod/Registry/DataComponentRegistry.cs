using Monocle;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#nullable enable

namespace Celeste.Mod.Registry {
    public class DataComponentInfo {
        public string? ModName;
        public string? Description;
    }
    public struct DebugModeDataComponentInfo {
        public DataComponentInfo? info;
        internal nint registryId;
        public bool unloaded;
        internal int knownUnloaded;
    }
    internal struct DebugModeDataComponent {
        internal object? content;
        internal nint registryId;
    }

    /// <summary>
    /// getter and setter for your registration.
    /// </summary>
    /// <typeparam name="T">Target entity type.</typeparam>
    /// <typeparam name="TRet">Attached data type.</typeparam>
    public abstract class DataComponentAccessor<T, TRet> where T : patch_Entity where TRet : class? {
        public TRet? GetValue(T entity) {
            return GetValueRefUnsafe(entity);
        }

        public void SetValue(T entity, TRet? value) {
            GetValueRefUnsafe(entity) = value;
        }

        internal abstract ref TRet? GetValueRefUnsafe(T entity);
    };

    public static class DataComponentRegistry {
        internal interface SlotHolderBase {
            internal int slot { set; }
            internal int knownCount { set; }
            internal abstract Type declaringType { get; }
            internal abstract Type fieldType { get; }
        }

        internal static int getHierarchyDepth(Type? self) {
            int depth = 0;
            while (self is not null) {
                self = self.BaseType;
                depth++;
            }
            return depth - 1;
        }

        internal static nint registryId = 1;
        internal static readonly Dictionary<Type, List<DebugModeDataComponentInfo>> debugInfos = new();
        internal static readonly Dictionary<Type, int> knownUnloadedIndex = new();

        internal static readonly Dictionary<Type, List<DataComponentInfo?>> infos = new();
        internal static Dictionary<Type, List<SlotHolderBase>>? toOptimize = new();

        internal static void Optimize() {
            if (toOptimize is not { } toop) {
                throw new InvalidOperationException("what did it mean");
            }
            toOptimize = null;
            Dictionary<Type, int> types = new();

            foreach ((Type k, List<DataComponentInfo?> o) in infos) {
                static int GetOrSet(Dictionary<Type, int> types, Type t, int? hint) {
                    if (types.TryGetValue(t, out int cur)) {
                        return cur;
                    }
                    int baseCnt;
                    if (t == typeof(Entity)) {
                        baseCnt = 0;
                    } else {
                        baseCnt = GetOrSet(types, t.BaseType!, null);
                    }
                    return types[t] = baseCnt + (hint ?? infos.GetValueOrDefault(t)?.Count ?? 0);
                }
                GetOrSet(types, k, o.Count);
            }
            foreach ((Type? k, List<SlotHolderBase>? v) in toop) {
                int all = types[k];
                int bas = all - v.Count;
                for (int i = 0; i < v.Count; i++) {
                    v[i].slot = bas + i;
                    v[i].knownCount = all;
                }
            }
        }

        internal static void ThrowNotPrepared() {
            throw new InvalidOperationException("It's not prepared.");
        }
        internal sealed class SlotHolder<T, TRet> : DataComponentAccessor<T, TRet>, SlotHolderBase
            where T : patch_Entity where TRet : class? {
            internal const int NotPrepared = -1;
            internal const int Unregistered = -2;
            internal override ref TRet? GetValueRefUnsafe(T self) {
                if (slot < 0) {
                    if (slot == -1) {
                        ThrowNotPrepared();
                    } else {
                        ThrowUnregistered();
                    }
                }
                ref object[] slots = ref self.slots;
                if (slots is not { }) {
                    slots = new object[knownCount];
                } else if (slots.Length <= slot) {
                    Array.Resize(ref slots, knownCount);
                }
                return ref Unsafe.As<object, TRet?>(ref slots[slot]);
            }

            internal void Unregister() {
                slot = Unregistered;
            }
            public Type declaringType => typeof(T);
            public Type fieldType => typeof(TRet);
            public int slot { get; set; } = NotPrepared;
            public int knownCount { get; set; }
        }

        internal static void ThrowUnregistered() {
            throw new ObjectDisposedException("It's already unregistered.");
        }
        internal sealed class DebugModeSlotHolder<T, TRet> : DataComponentAccessor<T, TRet> where T : patch_Entity where TRet : class? {
            internal required int depth;
            internal required int slot;
            internal required nint registryId;
            internal required List<DebugModeDataComponentInfo> registered;
            internal override ref TRet? GetValueRefUnsafe(T self) {
                if (slot < 0) {
                    ThrowUnregistered();
                }

                self.debugSlots ??= new DebugModeDataComponent[getHierarchyDepth(self.GetType())][];
                ref DebugModeDataComponent[]? curDepth = ref self.debugSlots[depth];

                if (curDepth is not { }) {
                    curDepth = new DebugModeDataComponent[registered.Count];
                } else if (curDepth.Length <= slot) {
                    Array.Resize(ref curDepth, registered.Count);
                }

                ref DebugModeDataComponent _got = ref curDepth[slot];
                ref object? got = ref _got.content;

                if (registryId != _got.registryId) {
                    if (registryId == registered[slot].registryId) {
                        got = null;
                    } else {
                        if (_got.registryId != 0) {
                            throw new InvalidOperationException("Unknown Error.");
                        }
                        _got.registryId = registryId;
                    }
                }

                if (got is { } && got is not TRet) {
                    throw new InvalidCastException();
                }
                return ref Unsafe.As<object?, TRet?>(ref got);
            }
        }

        internal static DataComponentAccessor<T, TRet> RegisterForDebug<T, TRet>(DataComponentInfo? info) where T : patch_Entity where TRet : class? {
            if (!debugInfos.TryGetValue(typeof(T), out List<DebugModeDataComponentInfo>? regList)) {
                regList = new();
                debugInfos.Add(typeof(T), regList);
            }
            nint id = registryId++;
            DebugModeDataComponentInfo debugInfo = new() { registryId = id, info = info, knownUnloaded = -1, unloaded = false };

            Span<DebugModeDataComponentInfo> reg = CollectionsMarshal.AsSpan(regList);
            if (knownUnloadedIndex.TryGetValue(typeof(T), out int i)) {
                int t = reg[i].knownUnloaded;
                if (t != -1) {
                    knownUnloadedIndex[typeof(T)] = t;
                } else {
                    knownUnloadedIndex.Remove(typeof(T));
                }
                reg[i] = debugInfo;
            } else {
                i = regList.Count;
                regList.Add(debugInfo);
            }

            return new DebugModeSlotHolder<T, TRet>() { registryId = id, slot = i, registered = regList, depth = getHierarchyDepth(typeof(T)) - 1, };
        }

        internal static DataComponentAccessor<T, TRet> RegisterForRelease<T, TRet>(DataComponentInfo? info) where T : patch_Entity where TRet : class? {
            if (toOptimize is not { } toop) {
                throw new InvalidOperationException("Slots have been frozen.");
            }
            if (!toop.TryGetValue(typeof(T), out List<SlotHolderBase>? holderList)) {
                holderList = new();
                toop.Add(typeof(T), holderList);
            }
            if (!infos.TryGetValue(typeof(T), out List<DataComponentInfo?>? regList)) {
                regList = new();
                infos.Add(typeof(T), regList);
            }
            var slot = new SlotHolder<T, TRet>();
            regList.Add(info);
            holderList.Add(slot);
            return slot;
        }

        /// <summary>
        /// A performant data holder implementation.
        /// Allows you to attach any data to a type of entity.
        /// </summary>
        /// <param name="info">
        /// it's mainly for external tools, and not actually used anywhere.
        /// type your modname and comment here.
        /// </param>
        /// <returns>The field accessor. note that your field can be null if it's not initialized.</returns>
        public static DataComponentAccessor<T, TRet> RegisterFor<T, TRet>(DataComponentInfo? info) where T : patch_Entity where TRet : class? {
            if (toOptimize is not { }) {
                return RegisterForDebug<T, TRet>(info);
            } else {
                return RegisterForRelease<T, TRet>(info);
            }
        }

        /// <summary>
        /// Unregister your accessor.
        /// </summary>
        public static void Unregister<T, TRet>(DataComponentAccessor<T, TRet> accessor) where T : patch_Entity where TRet : class? {
            if (accessor is not DebugModeSlotHolder<T, TRet> holder) {
                if (accessor is SlotHolder<T, TRet> releaseMode) {
                    releaseMode.Unregister();
                    return;
                }
                throw new ArgumentException("Not a valid accessor.");
            }
            Span<DebugModeDataComponentInfo> reg = CollectionsMarshal.AsSpan(holder.registered);
            int target = holder.slot;
            if (target < 0 || reg[target].unloaded) {
                return;
            }
            reg[target].unloaded = true;

            if (knownUnloadedIndex.TryGetValue(typeof(T), out int i)) {
                reg[target].knownUnloaded = i;
            }

            knownUnloadedIndex[typeof(T)] = target;
            holder.slot = -1;
        }
    }
}
