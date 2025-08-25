using Celeste.Mod.Helpers.LegacyMonoMod;
using MonoMod.Core.Platforms;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.Helpers;

public static class HookUtils {
    // This field is used to skip warnings on method which have had inlining disabled at runtime, since
    // reflection does not update the inlining status of a method.
    private static readonly HashSet<MethodBase> InliningDisabledMethods = new();
    
    /// <summary>
    /// Attempts to disable inlining on a method.
    /// </summary>
    /// <param name="targetMethod">The method to disable inlining on.</param>
    /// <returns>Whether it was successful.</returns>
    public static bool TryDisableInlining(MethodBase targetMethod) {
        if (InliningDisabledMethods.Contains(targetMethod)) return true;

        if (PlatformTriple.Current.TryDisableInlining(targetMethod)) {
            InliningDisabledMethods.Add(targetMethod);
            return true;
        }

        return false;
    }
    
    // Entirely disallow any sort of (managed) hooks on methods flagged as AggressiveInlining
    // Also warn on any late hooks on methods which are for inlining
    internal static void EnsureLegalHook(DetourBase info) {
        MethodBase target = info.Method.Method;
        if (target.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining)) return;
            
        MethodBase hookingMethod = info switch {
            DetourInfo di => di.Entry,
            ILHookInfo hi => hi.ManipulatorMethod,
            _ => throw new NotSupportedException(),
        };
        // Hooks on AggressiveInlining tagged methods are considered significantly performance impactful and are never allowed
        if (target.MethodImplementationFlags.HasFlag(MethodImplAttributes.AggressiveInlining)) {
            MonoModPolice.ReportMonoModCrime($"{GetFullMethodName(hookingMethod)} is hooking method {GetFullMethodName(target)} which has aggressive inlining enabled!", hookingMethod);
        }
            
        // We consider a hook to be "late" if the EverestModule `Initialize` call has occured already for all the loaded mods.
        // But leave the message for early hooks on verbose level too
        if (!InliningDisabledMethods.Contains(target))
            Logger.Log(Everest._Initialized ? LogLevel.Warn : LogLevel.Verbose, "Everest", $"{GetFullMethodName(hookingMethod)} is hooking method {GetFullMethodName(target)} which does not have inlining disabled!");
        return;

        string GetFullMethodName(MethodBase m) {
            return $"{m.DeclaringType?.FullName ?? "(Unknown Declaring Type}"}.{m.Name}";
        }
    }
}