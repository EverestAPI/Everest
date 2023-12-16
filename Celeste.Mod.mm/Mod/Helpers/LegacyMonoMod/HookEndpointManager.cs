
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using MonoMod;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    //This exists for two reasons:
    // - to give all hooks an (empty) config to bypass some hook ordering jank
    // - to "fix" the MonoMod crime of double hooks
    [ExternalGameDependencyPatchAttribute("MMHOOK_Celeste")]
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.HookGen.HookEndpointManager")]
    public static class LegacyHookEndpointManager {

        private static ConcurrentDictionary<(MethodBase, Delegate), Hook> Hooks = new ConcurrentDictionary<(MethodBase, Delegate), Hook>();
        private static ConcurrentDictionary<(MethodBase, Delegate), ILHook> ILHooks = new ConcurrentDictionary<(MethodBase, Delegate), ILHook>();

        private static bool IsEverestInternalMethod(MethodBase method) {
            for (Type type = method.DeclaringType; type != null; type = type.DeclaringType)
                if (type.Namespace.StartsWith("Celeste.Mod"))
                    return true;

            return false;
        }

        private static bool IsLegacyMMCaller() {
            foreach (StackFrame frame in new StackTrace().GetFrames())
                if (frame.HasMethod() && frame.GetMethod()?.DeclaringType?.Assembly is Assembly asm && AssemblyLoadContext.GetLoadContext(asm) is EverestModuleAssemblyContext ctx)
                    // Check if the mod was relinked from legacy MonoMod
                    return !ctx.ModuleMeta.IsNetCoreOnlyMod || asm.CustomAttributes.Any(attr => attr.AttributeType == typeof(RelinkedMonoModLegacyAttribute));
                    
            return false;
        }

        private static bool IsCoreModCaller() {
            foreach (StackFrame frame in new StackTrace().GetFrames())
                if (frame.HasMethod() && frame.GetMethod()?.DeclaringType?.Assembly is Assembly asm && AssemblyLoadContext.GetLoadContext(asm) is EverestModuleAssemblyContext ctx)
                    return ctx.ModuleMeta.IsNetCoreOnlyMod;
                    
            return false;
        }

        public static void Add<T>(MethodBase method, Delegate hookDelegate) where T : Delegate => Add(method, hookDelegate);
        public static void Add(MethodBase method, Delegate hookDelegate) {
            if (IsEverestInternalMethod(method) && IsCoreModCaller())
                throw new InvalidOperationException("Core mods may not add hooks to Everest internal methods");

            Hook hook = new Hook(method, hookDelegate, LegacyDetourContext.GetCurrentDetourConfig(false, IsLegacyMMCaller()));
            if (Hooks.TryAdd((method, hookDelegate), hook))
                return;
            hook.Dispose();

            // I wish we could just throw here...
            // What we must do for the sake of "backwards compatibility" ._.
            MonoModPolice.ReportMonoModCrime($"Double On. hook registered on method {method} (hook method: {hookDelegate.Method})", hookDelegate.Method);
        }

        public static void Remove<T>(MethodBase method, Delegate hookDelegate) where T : Delegate => Remove(method, hookDelegate);
        public static void Remove(MethodBase method, Delegate hookDelegate) {
            if (IsEverestInternalMethod(method) && IsCoreModCaller())
                throw new InvalidOperationException("Core mods may not remove hooks from Everest internal methods");

            if (Hooks.TryRemove((method, hookDelegate), out Hook hook))
                hook.Dispose();
        }

        public static void Modify<T>(MethodBase method, Delegate callback) where T : Delegate => Modify(method, callback);
        public static void Modify(MethodBase method, Delegate callback) {
            if (IsEverestInternalMethod(method) && IsCoreModCaller())
                throw new InvalidOperationException("Core mods may not add hooks to Everest internal methods");

            ILHook hook = new ILHook(method, (ILContext.Manipulator) callback, LegacyDetourContext.GetCurrentDetourConfig(true, IsLegacyMMCaller()));
            if (ILHooks.TryAdd((method, callback), hook))
                return;

            // I wish we could just throw here...
            // What we must do for the sake of "backwards compatibility" ._.
            MonoModPolice.ReportMonoModCrime($"Double IL. hook registered on method {method} (modifier method: {callback.Method})", callback.Method);
        }

        public static void Unmodify<T>(MethodBase method, Delegate callback) => Unmodify(method, callback);
        public static void Unmodify(MethodBase method, Delegate callback) {
            if (IsEverestInternalMethod(method) && IsCoreModCaller())
                throw new InvalidOperationException("Core mods may not remove hooks from Everest internal methods");

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