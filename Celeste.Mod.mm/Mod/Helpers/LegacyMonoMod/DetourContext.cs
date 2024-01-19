using MonoMod;
using MonoMod.Core;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.DetourContext")]
    public sealed class LegacyDetourContext : IDisposable {

        public static DetourConfig GetCurrentDetourConfig(bool forILHook, bool legacyDefault) {
            if (DetourContext.Current is LegacyDetourContext.ReorgContext legacyCtx)
                // Match legacy detour config behavior
                return legacyCtx.GetLegacyDetourConfig(forILHook ? LegacyILHook.AcquireGlobalIndex() : LegacyDetour.AcquireGlobalIndex(), forILHook);
            else if (DetourContext.CurrentConfig is DetourConfig reorgConfig)
                return reorgConfig;
            else if (!legacyDefault)
                return null;

            // Create a new empty legacy detour config
            uint globalIdx = forILHook ? LegacyILHook.AcquireGlobalIndex() : LegacyDetour.AcquireGlobalIndex();
            return CreateLegacyDetourConfig($"UNKNOWN_{(forILHook ? "ILHOOK" : "DETOUR")}${globalIdx}", 0, Enumerable.Empty<string>(), Enumerable.Empty<string>(), globalIdx, forILHook);
        }

        internal static DetourConfig CreateLegacyDetourConfig(string id, int prio, IEnumerable<string> before, IEnumerable<string> after, uint globalIdx, bool flipOrder = false) {
            // Handle wildcard Before / After
            int wcPrio = 0;

            if (before.Contains("*"))
                wcPrio = int.MinValue;

            if (after.Contains("*")) {
                if (wcPrio == 0)
                    wcPrio = int.MaxValue;
                else
                    MonoModPolice.ReportMonoModCrime($"Conflicting wildcard '*' in both DetourConfig '{id}' Before/After");
            }

            if (wcPrio != 0) {
                if (prio != 0)
                    Logger.Warn("legacy-monomod", $"Discarding DetourConfig '{id}' priority {prio} in favor of Before/After wildcard emulation priority {wcPrio}");

                prio = wcPrio;
            }

            // Flip priorities in case the hook order should be flipped (e.g. to emulate legacy ILHooks)
            if (flipOrder) {
                prio = unchecked(int.MaxValue - (prio - int.MinValue));
                globalIdx = uint.MaxValue - globalIdx;
                (before, after) = (after, before);
            }

            // Before / After are switched on reorg ._.
            return new DetourConfig(id, prio, after, before, unchecked(int.MinValue + (int) globalIdx));
        }

        private sealed class ReorgContext : DetourContext {

            public readonly LegacyDetourContext LegacyDetourContext;

            public ReorgContext(LegacyDetourContext legacyDetourCtx) => LegacyDetourContext = legacyDetourCtx;

            public DetourConfig GetLegacyDetourConfig(uint globalIdx, bool flipOrder)
                => LegacyDetourContext.CreateLegacyDetourConfig(LegacyDetourContext.ID, LegacyDetourContext.Priority, LegacyDetourContext.Before, LegacyDetourContext.After, globalIdx, flipOrder);

            protected override bool TryGetConfig(out DetourConfig config) {
                config = GetLegacyDetourConfig(0, false);
                return true;
            }

            protected override bool TryGetFactory(out IDetourFactory factory) {
                factory = null;
                return false;
            }

        }

        [ThreadStatic]
        private static List<LegacyDetourContext> _Contexts;
        private static List<LegacyDetourContext> Contexts => _Contexts ?? (_Contexts = new List<LegacyDetourContext>());

        [ThreadStatic]
        private static LegacyDetourContext Last;
        internal static LegacyDetourContext Current {
            get {
                if (Last?.IsValid ?? false)
                    return Last;

                // Find the most recently added valid context, remove invalid ones after it
                int valCtxIdx = Contexts.FindLastIndex(ctx => ctx.IsValid);
                Contexts.RemoveRange(valCtxIdx+1, Contexts.Count - (valCtxIdx+1));
                return Last = ((valCtxIdx >= 0) ? Contexts[valCtxIdx] : null);
            }
        }

        public int Priority;

        private readonly string _FallbackID;
        private string _ID;
        public string ID {
            get => _ID ?? _FallbackID;
            set => _ID = string.IsNullOrEmpty(value) ? null : value;
        }

        public List<string> Before = new List<string>(), After = new List<string>();

        public LegacyDetourConfig DetourConfig => new LegacyDetourConfig {
            Priority = Priority,
            ID = ID,
            Before = Before,
            After = After
        };

        public LegacyHookConfig HookConfig => new LegacyHookConfig {
            Priority = Priority,
            ID = ID,
            Before = Before,
            After = After
        };

        public LegacyILHookConfig ILHookConfig => new LegacyILHookConfig {
            Priority = Priority,
            ID = ID,
            Before = Before,
            After = After
        };

        private MethodBase Creator = null;
        private bool IsDisposed;
        internal bool IsValid {
            get {
                if (IsDisposed)
                    return false;

                if (Creator == null)
                    return true;

                // Check if the creator of this context has returned
                // This is jank as heck (what if the method is called again), but so is the entirety of legacy MonoMod .-.
                StackTrace stack = new StackTrace();
                int frameCount = stack.FrameCount;
                for (int i = 0; i < frameCount; i++)
                    if (stack.GetFrame(i).GetMethod() == Creator)
                        return true;

                return false;
            }
        }

        private IDisposable contextScope;

        public LegacyDetourContext(int prio, string id) {
            // Find the creator method
            StackTrace stack = new StackTrace();
            Creator = Enumerable.Range(0, stack.FrameCount).Select(i => stack.GetFrame(i).GetMethod()).FirstOrDefault(m => m?.DeclaringType != typeof(LegacyDetourContext));
            _FallbackID = Creator?.DeclaringType?.Assembly?.GetName().Name ?? Creator?.GetID(simple: true);

            // Add to the context list
            Last = this;
            Contexts.Add(this);

            Priority = prio;
            ID = id;

            // Start the context's scope (in case a legacy)
            contextScope = new ReorgContext(this).Use();
        }

        public LegacyDetourContext(string id) : this(0, id) {}
        public LegacyDetourContext(int priority) : this(priority, null) {}
        public LegacyDetourContext() : this(0, null) {}

        public void Dispose() {
            if (IsDisposed)
                return;
            IsDisposed = true;

            // End the context's scope
            contextScope?.Dispose();
            contextScope = null;

            // Remove from the context list
            Last = null;
            Contexts.Remove(this);
        }

    }
}
