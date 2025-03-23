using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MiniInstaller;

public static class DepCalls {
    public static Assembly AsmMonoMod;
    public static Assembly AsmHookGen;
    public static Assembly AsmNETCoreifier;
    
    public static void LoadModders() {
        if (AsmMonoMod != null && AsmNETCoreifier != null) return;
        // We can't add MonoMod as a reference to MiniInstaller, as we don't want to accidentally lock the file.
        // Instead, load it dynamically and invoke the entry point.
        // We also need to lazily load any dependencies.
        Logger.LogLine("Loading Mono.Cecil");
        LazyLoadAssembly(Path.Combine(Globals.PathGame, "Mono.Cecil.dll"));
        Logger.LogLine("Loading Mono.Cecil.Mdb");
        LazyLoadAssembly(Path.Combine(Globals.PathGame, "Mono.Cecil.Mdb.dll"));
        Logger.LogLine("Loading Mono.Cecil.Pdb");
        LazyLoadAssembly(Path.Combine(Globals.PathGame, "Mono.Cecil.Pdb.dll"));
        Logger.LogLine("Loading MonoMod.Utils.dll");
        LazyLoadAssembly(Path.Combine(Globals.PathGame, "MonoMod.Utils.dll"));
        Logger.LogLine("Loading MonoMod");
        AsmMonoMod ??= LazyLoadAssembly(Path.Combine(Globals.PathGame, "MonoMod.Patcher.dll"));
        Logger.LogLine("Loading MonoMod.RuntimeDetour.dll");
        LazyLoadAssembly(Path.Combine(Globals.PathGame, "MonoMod.RuntimeDetour.dll"));
        Logger.LogLine("Loading MonoMod.RuntimeDetour.HookGen");
        AsmHookGen ??= LazyLoadAssembly(Path.Combine(Globals.PathGame, "MonoMod.RuntimeDetour.HookGen.dll"));
        Logger.LogLine("Loading NETCoreifier");
        AsmNETCoreifier ??= LazyLoadAssembly(Path.Combine(Globals.PathGame, "NETCoreifier.dll"));
    }
    
    private static Assembly LazyLoadAssembly(string path) {
        Logger.LogLine($"Lazily loading {path}");
        ResolveEventHandler tmpResolver = (s, e) => {
            string asmPath = Path.Combine(Path.GetDirectoryName(path), new AssemblyName(e.Name).Name + ".dll");
            if (!File.Exists(asmPath))
                return null;
            return Assembly.LoadFrom(asmPath);
        };
        AppDomain.CurrentDomain.AssemblyResolve += tmpResolver;
        Assembly asm = Assembly.Load(Path.GetFileNameWithoutExtension(path));
        AppDomain.CurrentDomain.AssemblyResolve -= tmpResolver;
        AppDomain.CurrentDomain.TypeResolve += (s, e) => {
            return asm.GetType(e.Name) != null ? asm : null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            return e.Name == asm.FullName || e.Name == asm.GetName().Name ? asm : null;
        };
        return asm;
    }
    
    public static void RunMonoMod(string asmFrom, string asmTo = null, string[] dllPaths = null) {
        asmTo ??= asmFrom;
        dllPaths ??= new string[] { Globals.PathGame };

        Logger.LogLine($"Running MonoMod for {asmFrom}");

        string asmTmp = Path.Combine(Globals.PathTmp, Path.GetFileName(asmTo));
        try {
            // We're lazy.
            Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", Globals.PathGame);
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
            int returnCode = (int) AsmMonoMod.EntryPoint.Invoke(null, new object[] { Enumerable.Repeat(asmFrom, 1).Concat(dllPaths).Append(asmTmp).ToArray() });

            if (returnCode != 0)
                File.Delete(asmTmp);

            if (!File.Exists(asmTmp))
                throw new Exception($"MonoMod failed creating a patched assembly: exit code {returnCode}!");

            MiscUtil.MoveExecutable(asmTmp, asmTo);
        } finally {
            File.Delete(asmTmp);
            File.Delete(Path.ChangeExtension(asmTmp, "pdb"));
            File.Delete(Path.ChangeExtension(asmTmp, "mdb"));
        }
    }
    
    public static void RunHookGen(string asm, string targetName) {
        Logger.LogLine($"Running MonoMod.RuntimeDetour.HookGen for {asm}");
        // We're lazy.
        Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", Globals.PathGame);
        Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
        AsmHookGen.EntryPoint.Invoke(null, new object[] { new string[] { "--private", asm, Path.Combine(Path.GetDirectoryName(targetName), "MMHOOK_" + Path.ChangeExtension(Path.GetFileName(targetName), "dll")) } });
    }
    
    public static void ConvertToNETCore(string asmFrom, string asmTo = null, HashSet<string> convertedAsms = null) {
        asmTo ??= asmFrom;
        convertedAsms ??= new HashSet<string>();

        if (!convertedAsms.Add(asmFrom))
            return;

        // Convert dependencies first
        string[] deps = MiscUtil.GetPEAssemblyReferences(asmFrom).Keys.ToArray();

        if (deps.Contains("NETCoreifier"))
            return; // Don't convert an assembly twice

        foreach (string dep in deps) {
            string srcDepPath = Path.Combine(Path.GetDirectoryName(asmFrom), $"{dep}.dll");
            string dstDepPath = Path.Combine(Path.GetDirectoryName(asmTo), $"{dep}.dll");
            if (File.Exists(srcDepPath) && !MiscUtil.IsSystemLibrary(srcDepPath))
                ConvertToNETCore(srcDepPath, dstDepPath, convertedAsms);
            else if (File.Exists(dstDepPath) && !MiscUtil.IsSystemLibrary(srcDepPath))
                ConvertToNETCore(dstDepPath, convertedAsms: convertedAsms);
        }

        ConvertToNETCoreSingle(asmFrom, asmTo);
    }

    public static void ConvertToNETCoreSingle(string asmFrom, string asmTo) {
        Logger.LogLine($"Converting {asmFrom} to .NET Core");
        
        string asmTmp = Path.Combine(Globals.PathTmp, Path.GetFileName(asmTo));
        try {
            AsmNETCoreifier.GetType("NETCoreifier.Coreifier")
                .GetMethod("ConvertToNetCore", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null)
                .Invoke(null, new object[] { asmFrom, asmTmp });

            MiscUtil.MoveExecutable(asmTmp, asmTo);
        } finally {
            File.Delete(asmTmp);
            File.Delete(Path.ChangeExtension(asmTmp, "pdb"));
            File.Delete(Path.ChangeExtension(asmTmp, "mdb"));
        }
    }
}