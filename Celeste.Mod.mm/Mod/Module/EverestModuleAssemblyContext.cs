using Celeste.Mod.Helpers;
using Ionic.Zip;
using Mono.Cecil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Celeste.Mod {
    /// <summary>
    /// A mods assembly context, which handles resolving/loading mod assemblies
    /// </summary>
    public sealed class EverestModuleAssemblyContext : AssemblyLoadContext, IAssemblyResolver {

        private static readonly Dictionary<string, AssemblyDefinition> _GlobalAssemblyResolveCache = new Dictionary<string, AssemblyDefinition>();

        private readonly object LOCK = new object();

        public readonly EverestModuleMetadata ModuleMeta;
        internal readonly List<EverestModuleAssemblyContext> DependencyContexts = new List<EverestModuleAssemblyContext>();

        private readonly string _ModAsmDir;
        private readonly Dictionary<string, Assembly> _LoadedAssemblies = new Dictionary<string, Assembly>();
        private readonly Dictionary<string, ModuleDefinition> _AssemblyModules = new Dictionary<string, ModuleDefinition>();

        private readonly ConcurrentDictionary<string, Assembly> _AssemblyLoadCache = new ConcurrentDictionary<string, Assembly>();
        private readonly ConcurrentDictionary<string, AssemblyDefinition> _AssemblyResolveCache = new ConcurrentDictionary<string, AssemblyDefinition>();

        private bool isDisposed = false;

        internal EverestModuleAssemblyContext(EverestModuleMetadata meta) : base(meta.Name, true) {
            ModuleMeta = meta;

            // Determine assembly directory
            if (!string.IsNullOrEmpty(meta.DLL)) {
                if (!string.IsNullOrEmpty(meta.PathDirectory))
                    _ModAsmDir = Path.GetDirectoryName(meta.DLL);
                else
                    _ModAsmDir = Path.GetDirectoryName(meta.DLL.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            }

            // Resolve dependecies
            foreach (EverestModuleMetadata dep in meta.Dependencies)
                if (Everest._ModulesByName.TryGetValue(dep.Name, out EverestModule module) && module.Metadata.AssemblyContext != null)
                    DependencyContexts.Add(module.Metadata.AssemblyContext);

            foreach (EverestModuleMetadata dep in meta.OptionalDependencies)
                if (Everest._ModulesByName.TryGetValue(dep.Name, out EverestModule module) && module.Metadata.AssemblyContext != null)
                    DependencyContexts.Add(module.Metadata.AssemblyContext);
        }

        public void Dispose() {
            lock (LOCK) {
                if (isDisposed)
                    return;
                isDisposed = true;

                // Unload all assemblies loaded in the context
                foreach (ModuleDefinition module in _AssemblyModules.Values)
                    module.Dispose();
                _AssemblyModules.Clear();

                foreach (Assembly asm in Assemblies)
                    Everest.UnloadAssembly(ModuleMeta, asm);            
                _LoadedAssemblies.Clear();

                _AssemblyLoadCache.Clear();
                _AssemblyResolveCache.Clear();
            }

            Unload();
        }

        /// <summary>
        /// Tries to load an assembly from a given path inside the mod.
        /// This path is an absolute path if the the mod was loaded from a directory, or a path into the mod ZIP otherwise.
        /// </summary>
        /// <param name="path">The path to load the assembly from</param>
        /// <param name="asmName">The assembly name, or null for the default</param>
        /// <returns></returns>
        public Assembly LoadAssemblyFromModPath(string path, string asmName = null) {
            lock (LOCK) {
                if (isDisposed)
                    throw new ObjectDisposedException(nameof(EverestModuleAssemblyContext));

                // Determine the default assembly name
                if (asmName == null)
                    asmName = Path.GetFileNameWithoutExtension(path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));

                // Check if the assembly has already been loaded
                if (_LoadedAssemblies.TryGetValue(path, out Assembly asm))
                    return asm;

                // Temporarily make the assembly resolve to null while actually loading it
                // This can fix self referential assemblies blowing up
                _LoadedAssemblies.Add(path, null);

                // Try to load + relink the assembly
                // Do this on the main thread, as otherwise stuff can break
                if (!string.IsNullOrEmpty(ModuleMeta.PathArchive))
                    using (ZipFile zip = new ZipFile(ModuleMeta.PathArchive)) {
                        // Try to find + load the entry
                        path = path.Replace('\\', '/');
                        ZipEntry entry = zip.Entries.FirstOrDefault(entry => entry.FileName == path);

                        if (entry != null)
                            using (Stream stream = entry.ExtractStream())
                                asm = Everest.Relinker.GetRelinkedAssembly(ModuleMeta, asmName, stream);
                    }
                else if (!string.IsNullOrEmpty(ModuleMeta.PathDirectory))
                    if (File.Exists(path))
                        using (Stream stream = File.OpenRead(path))
                            asm = Everest.Relinker.GetRelinkedAssembly(ModuleMeta, asmName, stream);

                // Actually add the assembly to list of loaded assemblies if we managed to load it
                if (asm != null) {
                    _LoadedAssemblies[path] = asm;
                    Logger.Log(LogLevel.Info, "modasmctx", $"Loaded assembly {asm.FullName} from module '{ModuleMeta.Name}' path '{path}'");
                }

                return asm;
            }
        }

        /// <summary>
        /// Loads a relinked assembly into this load context
        /// </summary>
        internal Assembly LoadRelinkedAssembly(string path) {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(EverestModuleAssemblyContext));

            ModuleDefinition mod = null;
            try {
                // Load the module + assembly
                mod = ModuleDefinition.ReadModule(path);
                Assembly asm = LoadFromAssemblyPath(path);

                // Insert into dictionaries
                string asmName = asm.GetName().Name;
                _AssemblyModules.Add(asmName, mod);
                _AssemblyLoadCache.TryAdd(asmName, asm);
                _AssemblyResolveCache.TryAdd(asmName, mod.Assembly);

                return asm;
            } catch {
                mod?.Dispose();
                throw;
            }
        }

        protected override Assembly Load(AssemblyName asmName) {
            // Lookup in the cache
            if (_AssemblyLoadCache.TryGetValue(asmName.Name, out Assembly cachedAsm))
                return cachedAsm;

            // Try to load the assembly
            Assembly asm = LoadUncached(asmName);
            _AssemblyLoadCache.TryAdd(asmName.Name, asm);

            if (asm == null)
                Logger.Log(LogLevel.Warn, "modasmctx", $"Failed to load assembly '{asmName.FullName}' for module '{ModuleMeta.Name}'");

            return asm;
        }

        protected override IntPtr LoadUnmanagedDll(string dllName) => IntPtr.Zero;

        /// <summary>
        /// Resolves an assembly name reference to an assembly definition
        /// </summary>
        public AssemblyDefinition Resolve(AssemblyNameReference asmName) {
            // Lookup in the cache
            if (_AssemblyResolveCache.TryGetValue(asmName.Name, out AssemblyDefinition cachedAsm))
                return cachedAsm;

            // Try to resolve the assembly
            AssemblyDefinition asm = ResolveUncached(asmName);
            _AssemblyResolveCache.TryAdd(asmName.Name, asm);

            // No warning if we failed to resolve it - the relinker will emit its own warning if needed + there's another warning upon an actual load failure

            return asm;
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => Resolve(name);

        private Assembly LoadUncached(AssemblyName asmName) {
            // Try to load the assembly from this mod
            if (LoadFromThisMod(asmName) is Assembly asm)
                return asm;

            // Try to load the assembly from dependency assembly contexts
            foreach (EverestModuleAssemblyContext depCtx in DependencyContexts) {
                try {
                    if (depCtx.LoadFromAssemblyName(asmName) is Assembly depAsm)
                        return depAsm;
                } catch {}
            }

            // Check if we can load this assembly from another module
            // If yes add its context as a dependency
            foreach (EverestModule module in Everest.Modules)
                if (module.Metadata.AssemblyContext?.LoadFromThisMod(asmName) is Assembly moduleAsm) {
                    Logger.Log(LogLevel.Info, "modasmctx", $"Loading assembly '{asmName.FullName}' from non-dependency '{module.Metadata.Name}' for module '{ModuleMeta.Name}'");
                    DependencyContexts.Add(module.Metadata.AssemblyContext);
                    return moduleAsm;
                }

            // Try to load the assembly from the default assembly load context
            try {
                if (AssemblyLoadContext.Default.LoadFromAssemblyName(asmName) is Assembly globalAsm)
                    return globalAsm;
            } catch {}

            return null;
        }

        private AssemblyDefinition ResolveUncached(AssemblyNameReference asmName) {
            // Try to resolve the assembly in this mod
            if (ResolveFromThisMod(asmName) is AssemblyDefinition asm)
                return asm;

            // Try to resolve the assembly in dependency assembly contexts
            foreach (EverestModuleAssemblyContext depCtx in DependencyContexts)
                if (depCtx.Resolve(asmName) is AssemblyDefinition depAsm)
                    return depAsm;

            // Check if we can resolve this assembly in another module
            // If yes add its context as a dependency
            foreach (EverestModule module in Everest.Modules)
                if (module.Metadata.AssemblyContext?.ResolveFromThisMod(asmName) is AssemblyDefinition moduleAsm) {
                    Logger.Log(LogLevel.Info, "modasmctx", $"Resolving assembly '{asmName.FullName}' in non-dependency '{module.Metadata.Name}' for module '{ModuleMeta.Name}'");
                    DependencyContexts.Add(module.Metadata.AssemblyContext);
                    return moduleAsm;
                }

            // Try to resolve a global assembly definition
            if (!_GlobalAssemblyResolveCache.TryGetValue(asmName.Name, out AssemblyDefinition globalAsmDef)) {
                // Try to load the global assembly
                Assembly globalAsm = null;
                try {
                    globalAsm = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(asmName.Name));
                } catch {}

                // Try to read its module
                globalAsmDef = null;
                if (globalAsm?.Location is string globalAsmLoc) {
                    try {
                        globalAsmDef = ModuleDefinition.ReadModule(globalAsmLoc).Assembly;
                    } catch (Exception e) {
                        Logger.Log(LogLevel.Warn, "modasmctx", $"Failed to resolve global assembly definition '{asmName.FullName}'");
                        e.LogDetailed();
                    }
                }
             
                // Add to cache
                _GlobalAssemblyResolveCache.Add(asmName.Name, globalAsmDef);
            }

            if (globalAsmDef != null)
                return globalAsmDef;

            return null;
        }

        private Assembly LoadFromThisMod(AssemblyName asmName) {
            if (_ModAsmDir == null)
                return null;

            // Try to load the assembly from the same directory as the main dll
            if (LoadAssemblyFromModPath(Path.Combine(_ModAsmDir, $"{asmName.Name}.dll")) is Assembly loadAsm)
                return loadAsm;

            return null;
        }

        private AssemblyDefinition ResolveFromThisMod(AssemblyNameReference asmName) {
            if (_ModAsmDir == null)
                return null;

            // Try to load the assembly from the same directory as the main dll
            if (LoadAssemblyFromModPath(Path.Combine(_ModAsmDir, $"{asmName.Name}.dll")) != null)
                return _AssemblyModules[asmName.Name].Assembly;

            return null;
        }

    }
}