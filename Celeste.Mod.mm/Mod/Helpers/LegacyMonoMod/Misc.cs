using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class RelinkedMonoModLegacyAttribute : Attribute {}

    internal static class LegacyMonoModCompatLayer {
        public static void Initialize() {
            LegacyDynamicDataCompatHooks.InstallHook();
            Logger.Info("legacy-monomod", "Initialized legacy MonoMod compatibility layer");
        }

        public static void Uninitialize() {
            LegacyDynamicDataCompatHooks.UninstallHook();
            Logger.Info("legacy-monomod", "Uninitialized legacy MonoMod compatibility layer");
        }
    }

    public static class ILShims {
        //Relinking is done using a MonoModder hackfix because the methods are generic ._.
        public static int ILCursor_AddReference<T>(ILCursor cursor, T t) => cursor.AddReference(in t); // Reorg expects an in-argument
        public static int ILCursor_EmitReference<T>(ILCursor cursor, T t) => cursor.EmitReference(in t); // Reorg expects an in-argument
    }

    [RelinkLegacyMonoMod("MonoMod.Utils.PlatformHelper")]
    public static class LegacyPlatformHelper {

        [Flags]
        [RelinkLegacyMonoMod("MonoMod.Utils.Platform")]
        public enum Platform : int {
            OS = 1 << 0,
            Bits64 = 1 << 1,
            NT = 1 << 2,
            Unix = 1 << 3,
            ARM = 1 << 16,
            Wine = 1 << 17,
            Unknown = OS | (1 << 4),
            Windows = OS | NT | (1 << 5),
            MacOS = OS | Unix | (1 << 6),
            Linux = OS | Unix | (1 << 7),
            Android = Linux | (1 << 8),
            iOS = MacOS | (1 << 9),
        }

        private static Platform? _current;
        public static Platform Current {
            get => _current ??=
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Platform.Windows :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Platform.MacOS :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Platform.Linux :
                    Platform.Unknown;
            set => throw new NotSupportedException("PlatformHelper.set_Current is no longer supported");
        }

        public static bool Is(Platform platform) => (Current & platform) == platform;

        private static string _librarySuffix;
        public static string LibrarySuffix => _librarySuffix ??= Is(Platform.MacOS) ? "dylib" : Is(Platform.Unix) ? "so" : "dll";

    }

    internal static class MonoModPolice {

        public sealed class MonoModCrimeException : Exception {
            public MonoModCrimeException(string descr) : base($"MONOMOD CRIME DETECTED - THIS IS A BUG: {descr}") {}
        }

        private static HashSet<(string descr, Assembly perpetrator, string backtrace)> loggedCrimes = new();

        public static void ReportMonoModCrime(string descr, MethodBase perpetrator) {
            Module perpetratorMod = null;
            try {
                perpetratorMod = perpetrator.Module; // This can throw, so just to be safe wrap it in a try-catch
            } catch {}
            ReportMonoModCrime(descr, perpetratorMod);
        }

        public static void ReportMonoModCrime(string descr) => ReportMonoModCrime(descr, (Assembly) null);
        public static void ReportMonoModCrime(string descr, Module perpetrator) => ReportMonoModCrime(descr, perpetrator?.Assembly);
        public static void ReportMonoModCrime(string descr, Assembly perpetrator) {
            // Check if we can trace this back to an offending mod
            EverestModuleMetadata perpetratorMeta = null;
            if (perpetrator != null)
                perpetratorMeta = (AssemblyLoadContext.GetLoadContext(perpetrator) as EverestModuleAssemblyContext)?.ModuleMeta;

            // This means that a mod did something objectively wrong (=a bug in the mod)
            // But because it "used to worked":tm:, we can't give them the crash they deserve
            // So we at least yell at them loudly in the log file (once) ._.
            if (!string.IsNullOrWhiteSpace(perpetratorMeta?.PathDirectory) || loggedCrimes.Add((descr, perpetrator, Environment.StackTrace))) {
                Logger.Error("legacy-monomod", "##################################################################################");
                Logger.Error("legacy-monomod", "                              MONOMOD CRIME DETECTED                              ");
                Logger.Error("legacy-monomod", "##################################################################################");
                Logger.Error("legacy-monomod", "                 !!! This means one of your mods has a bug !!!                    ");
                Logger.Error("legacy-monomod", "   However, for the sake of backwards compatibility, a crash has been prevented   ");
                Logger.Error("legacy-monomod", "      Please report this to the mod author so that they can fix their mod!        ");
                Logger.Error("legacy-monomod", "");
                if (perpetratorMeta != null)
                    Logger.Error("legacy-monomod", $"Suspected perpetrator: {perpetratorMeta.Name} version {perpetratorMeta.VersionString} [{perpetratorMeta.Version}]");
                Logger.Error("legacy-monomod", $"Details of infraction: {descr}");
                Logger.LogDetailed(LogLevel.Error, "legacy-monomod", $"Stacktrace:");
            }

            // If we know that the offender is a directory mod (which implies that this is a mod dev), still crash >:)
            if (!string.IsNullOrEmpty(perpetratorMeta?.PathDirectory))
                throw new MonoModCrimeException(descr);
        }

    }
}

namespace MonoMod {
    partial class MonoModRules {
        private static void SetupLegacyMonoModPatcherHackfixes(MonoModder modder, AssemblyNameReference celesteRef) {
            // Dear MonoMod.Patcher,
            // What the f*ck is this supposed to be!?!?!?
            // Sincerely, me :)
            // (yes this tells MonoMod to relink ILLabel::Target to ILLabel::Target, and yes this is required)
            modder.RelinkMap["Mono.Cecil.Cil.Instruction MonoMod.Cil.ILLabel::Target"] = new RelinkMapEntry("MonoMod.Cil.ILLabel", "Target");

            OnPostProcessMethod += (modder, method) => {
                if (!method.HasBody)
                    return;

                // We have to do our own relinking because who the heck would ever want to relink a generic method???????
                TypeDefinition ilShimsType = RulesModule.GetType("Celeste.Mod.Helpers.LegacyMonoMod.ILShims");
                foreach (Instruction instr in method.Body.Instructions) {
                    if (!(instr.Operand is MethodReference mref))
                        continue;

                    // Check if this is the correct method
                    if (!mref.HasThis || !mref.IsGenericInstance || mref.Parameters.Count != 1)
                        continue;

                    if (!(mref is GenericInstanceMethod mrefGenInst) || mrefGenInst.GenericArguments.Count != 1)
                        continue;

                    if (mref.DeclaringType.FullName != "MonoMod.Cil.ILCursor")
                        continue;

                    if (mref.Name != "AddReference" && mref.Name != "EmitReference")
                        continue;

                    if (mref.Parameters[0].ParameterType.IsByReference)
                        continue;

                    // Relink the reference
                    GenericInstanceMethod genericInst = new GenericInstanceMethod(ilShimsType.FindMethod($"ILCursor_{mref.Name}"));
                    genericInst.GenericArguments.AddRange(mrefGenInst.GenericArguments);

                    mref = modder.Module.ImportReference(genericInst);
                    mref.DeclaringType.Scope = celesteRef; // Fix the reference scope: Celeste.Mod.mm.dll -> Celeste.dll
                    instr.Operand = mref;
                }
            };
        }
    }
}