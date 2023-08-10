
using System;
using System.Collections.Concurrent;
using System.Reflection;
using MonoMod;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    //This exists for two reasons:
    // - to give all hooks an (empty) config to bypass some hook ordering jank
    // - to "fix" the MonoMod crime of double hooks
    [ExternalGameDependencyPatch("MMHOOK_Celeste")]
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.HookGen.HookEndpointManager")]
    public static class LegacyHookEndpointManager {

        private static readonly DetourConfig EmptyConfig = new DetourConfig("MMHOOK_HOOK", 0);

        private static ConcurrentDictionary<(MethodBase, Delegate), Hook> Hooks = new ConcurrentDictionary<(MethodBase, Delegate), Hook>();
        private static ConcurrentDictionary<(MethodBase, Delegate), ILHook> ILHooks = new ConcurrentDictionary<(MethodBase, Delegate), ILHook>();

        public static void Add<T>(MethodBase method, Delegate hookDelegate) where T : Delegate => Add(method, hookDelegate);
        public static void Add(MethodBase method, Delegate hookDelegate) {
            Hook hook = new Hook(method, hookDelegate, DetourContext.CurrentConfig ?? EmptyConfig);
            if (Hooks.TryAdd((method, hookDelegate), hook))
                return;
            hook.Dispose();

            // I wish we could just throw here...
            // What we must do for the sake of "backwards compatibility" ._.
            MonoModPolice.ReportMonoModCrime($"Double On. hook registered on method {method} (hook method: {hookDelegate.Method})", hookDelegate.Method);
        }

        public static void Remove<T>(MethodBase method, Delegate hookDelegate) where T : Delegate => Remove(method, hookDelegate);
        public static void Remove(MethodBase method, Delegate hookDelegate){
            if (Hooks.TryRemove((method, hookDelegate), out Hook hook))
                hook.Dispose();
        }

        public static void Modify<T>(MethodBase method, Delegate callback) where T : Delegate => Modify(method, callback);
        public static void Modify(MethodBase method, Delegate callback) {
            ILHook hook = new ILHook(method, (ILContext.Manipulator) callback, DetourContext.CurrentConfig ?? EmptyConfig);
            if (ILHooks.TryAdd((method, callback), hook))
                return;

            // I wish we could just throw here...
            // What we must do for the sake of "backwards compatibility" ._.
            MonoModPolice.ReportMonoModCrime($"Double IL. hook registered on method {method} (modifier method: {callback.Method})", callback.Method);
        }

        public static void Unmodify<T>(MethodBase method, Delegate callback) => Unmodify(method, callback);
        public static void Unmodify(MethodBase method, Delegate callback) {
            if (ILHooks.TryRemove((method, callback), out ILHook hook))
                hook.Dispose();
        }

        public static void Clear() {
            foreach (Hook hook in Hooks.Values)
                hook.Dispose();
            Hooks.Clear();

            foreach (ILHook hook in ILHooks.Values)
                hook.Dispose();
            ILHooks.Clear();
        }
    }
}