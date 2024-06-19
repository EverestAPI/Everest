using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Ionic.Zip;
using Mono.Cecil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading;

namespace Celeste.Mod {
    /// <summary>
    /// A module's assembly context, which handles resolving/loading mod assemblies
    /// </summary>
    public sealed class EverestModuleAssemblyContext : AssemblyLoadContext, IAssemblyResolver {

        /// <summary>
        /// A list of assembly names which must not be loaded by a mod. The list will be initialized upon first access (which is before any mods will have loaded).
        /// </summary>
        internal static string[] AssemblyLoadBlackList => _AssemblyLoadBlackList ?? (_AssemblyLoadBlackList = 
            AssemblyLoadContext.Default.Assemblies.Select(asm => asm.GetName().Name)
            .Append("Mono.Cecil.Pdb").Append("Mono.Cecil.Mdb") // These two aren't picked up by default for some reason
            .ToArray()
        );
        private static string[] _AssemblyLoadBlackList;

        /// <summary>
        /// The folder name where mod unmanaged assemblies will be loaded from.
        /// </summary>
        internal static string UnmanagedLibraryFolder = 
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (Environment.Is64BitOperatingSystem ? "lib-win-x64" : "lin-win-x86") :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "lib-linux" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "lib-osx" :
            null
        ;

        internal static readonly ReaderWriterLockSlim _AllContextsLock = new ReaderWriterLockSlim();
        internal static readonly LinkedList<EverestModuleAssemblyContext> _AllContexts = new LinkedList<EverestModuleAssemblyContext>();
        internal static readonly Dictionary<string, EverestModuleAssemblyContext> _ContextsByName = new Dictionary<string, EverestModuleAssemblyContext>();

        private static readonly Dictionary<string, AssemblyDefinition> _GlobalAssemblyResolveCache = new Dictionary<string, AssemblyDefinition>();

        [ThreadStatic]
        private static Stack<EverestModuleAssemblyContext> _ActiveLocalLoadContexts;

        private readonly object LOCK = new object();

        public readonly EverestModuleMetadata ModuleMeta;
        internal readonly List<EverestModuleAssemblyContext> DependencyContexts = new List<EverestModuleAssemblyContext>();
        internal readonly HashSet<EverestModuleAssemblyContext> ActiveDependencyContexts = new HashSet<EverestModuleAssemblyContext>();

        private readonly string _ModAsmDir;
        private readonly Dictionary<string, Assembly> _LoadedAssemblies = new Dictionary<string, Assembly>();
        private readonly Dictionary<string, ModuleDefinition> _AssemblyModules = new Dictionary<string, ModuleDefinition>();
        private readonly Dictionary<string, FileSystemWatcher> _AssemblyReloadWatchers = new Dictionary<string, FileSystemWatcher>();

        private readonly ConcurrentDictionary<string, Assembly> _AssemblyLoadCache = new ConcurrentDictionary<string, Assembly>();
        private readonly ConcurrentDictionary<string, IntPtr> _AssemblyUnmanagedLoadCache = new ConcurrentDictionary<string, IntPtr>();
        private readonly ConcurrentDictionary<string, AssemblyDefinition> _AssemblyResolveCache = new ConcurrentDictionary<string, AssemblyDefinition>();

        private readonly ConcurrentDictionary<string, Assembly> _LocalLoadCache = new ConcurrentDictionary<string, Assembly>();
        private readonly ConcurrentDictionary<string, IntPtr> _LocalUnmanagedLoadCache = new ConcurrentDictionary<string, IntPtr>();
        private readonly ConcurrentDictionary<string, AssemblyDefinition> _LocalResolveCache = new ConcurrentDictionary<string, AssemblyDefinition>();

        private LinkedListNode<EverestModuleAssemblyContext> listNode;
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

            // Resolve dependencies
            lock (Everest._Modules) {
                foreach (EverestModuleMetadata dep in meta.Dependencies)
                    if (_ContextsByName.TryGetValue(dep.Name, out EverestModuleAssemblyContext alc))
                        DependencyContexts.Add(alc);

                foreach (EverestModuleMetadata dep in meta.OptionalDependencies)
                    if (_ContextsByName.TryGetValue(dep.Name, out EverestModuleAssemblyContext alc))
                        DependencyContexts.Add(alc);
            }

            // Add to mod ALC list
            _AllContextsLock.EnterWriteLock();
            try {
                listNode = _AllContexts.AddLast(this);
                _ContextsByName.Add(meta.Name, this);
            } finally {
                _AllContextsLock.ExitWriteLock();
            }
        }

        public void Dispose() {
            lock (LOCK) {
                if (isDisposed)
                    return;
                isDisposed = true;

                // Remove from mod ALC list
                _AllContextsLock.EnterWriteLock();
                try {
                    _AllContexts.Remove(listNode);
                    _ContextsByName.Remove(ModuleMeta.Name);
                    listNode = null;
                } finally {
                    _AllContextsLock.ExitWriteLock();
                }

                // Dispose all code reload file system watchers
                foreach (FileSystemWatcher watcher in _AssemblyReloadWatchers.Values)
                    watcher.Dispose();
                _AssemblyReloadWatchers.Clear();

                // Unload all assemblies loaded in the context
                foreach (ModuleDefinition module in _AssemblyModules.Values)
                    module.Dispose();
                _AssemblyModules.Clear();

                foreach (Assembly asm in Assemblies)
                    Everest.UnloadAssembly(ModuleMeta, asm);            
                _LoadedAssemblies.Clear();
    
                _AssemblyLoadCache.Clear();
                _LocalLoadCache.Clear();
                _AssemblyResolveCache.Clear();
            }

            Unload();
        }

        internal void PostBootCleanup() {
            // Clear the assembly resolves caches
            // They are primarily used by the relinker, and as such not needed anymore once the game is running
            _AssemblyResolveCache.Clear();
            _LocalResolveCache.Clear(); 
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
                string osSepPath = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                if (asmName == null)
                    asmName = Path.GetFileNameWithoutExtension(osSepPath);

                // Check if the assembly has already been loaded
                string asmPath = path.Replace('\\', '/');
                if (_LoadedAssemblies.TryGetValue(asmPath, out Assembly asm))
                    return asm;

                // Temporarily make the assembly resolve to null while actually loading it
                // This can fix self referential assemblies blowing up
                _LoadedAssemblies.Add(asmPath, null);

                // Determine cache paths
                string cachePath = GetCachedPath(ModuleMeta, asmName);
                string cacheChecksumPath = Path.ChangeExtension(cachePath, ".sum");

                // Calculate checksums
                string[] checksums = { Everest.Relinker.GameChecksum, ModuleMeta.Hash.ToHexadecimalString() };

                // Try to load + relink the assembly
                // Do this on the main thread, as otherwise stuff can break
                Stack<EverestModuleAssemblyContext> prevCtxs = _ActiveLocalLoadContexts;
                _ActiveLocalLoadContexts = null;
                try {
                    if (TryLoadCachedAssembly(ModuleMeta, asmName, cachePath, cacheChecksumPath, checksums) is not { } relinkedAsm) {
                        File.Delete(cachePath);
                        File.Delete(cacheChecksumPath);

                        (string asmFilePath, string symPath) = GetAssemblyFilePathsForRelinking(asmPath);

                        if (asmFilePath == null) {
                            return null;
                        }

                        relinkedAsm = Everest.Relinker.GetRelinkedAssembly(ModuleMeta, asmName, asmFilePath, symPath, cachePath, out string tmpOutPath);
                        
                        // Write the checksums for the cached assembly to be loaded in the future
                        // Skip this step if the relinker had to fall back to using a temporary output file
                        if (tmpOutPath == null) {
                            File.WriteAllLines(cacheChecksumPath, checksums);
                        }
                    }

                    asm = relinkedAsm;
                } finally {
                    _ActiveLocalLoadContexts = prevCtxs;
                }

                // Actually add the assembly to list of loaded assemblies if we managed to load it
                if (asm != null) {
                    _LoadedAssemblies[asmPath] = asm;

                    // Watch the assembly directory for changes if code mod reloading is enabled
                    if (!string.IsNullOrEmpty(ModuleMeta.PathDirectory) && ModuleMeta.SupportsCodeReload && CoreModule.Settings.CodeReload)
                        RegisterCodeReloadWatcher(Path.GetFullPath(Path.GetDirectoryName(path)));

                    Logger.Log(LogLevel.Info, "modasmctx", $"Loaded assembly {asm.FullName} from module '{ModuleMeta.Name}' path '{path}'");
                }

                return asm;
            }
        }

        private (string, string) GetAssemblyFilePathsForRelinking(string assemblyPath) {
            string symPath = Path.ChangeExtension(assemblyPath, "pdb");

            if (ModuleMeta.PathDirectory != null) {
                return (File.Exists(assemblyPath) ? assemblyPath : null, File.Exists(symPath) ? symPath : null);
            }

            if (ModuleMeta.PathArchive != null) {
                using ZipFile zip = new(ModuleMeta.PathArchive);

                // Try to find + load the (symbol) entry
                string entryPath = assemblyPath.Replace(Path.DirectorySeparatorChar, '/');
                ZipEntry entry = zip.Entries.FirstOrDefault(entry => entry.FileName == entryPath);

                string symEntryPath = symPath.Replace(Path.DirectorySeparatorChar, '/');
                ZipEntry symEntry = zip.Entries.FirstOrDefault(entry => entry.FileName == symEntryPath);

                if (entry == null) {
                    return default;
                }

                var asmZipStream = entry.ExtractStream();
                var symZipStream = symEntry?.ExtractStream();

                string assemblyOutPath = Path.GetTempFileName();
                using (FileStream tmpFs = File.OpenWrite(assemblyOutPath))
                    asmZipStream.CopyTo(tmpFs);

                string symOutPath = null;
                if (symZipStream != null) {
                    symOutPath = Path.GetTempFileName();
                    using (FileStream tmpFs = File.OpenWrite(symOutPath))
                        symZipStream.CopyTo(tmpFs);
                }

                return (assemblyOutPath, symOutPath);
            }

            return default;
        }
        
        /// <summary>
        /// Get the cached path of a given mod's relinked .dll
        /// </summary>
        /// <param name="meta">The mod metadata.</param>
        /// <param name="asmname"></param>
        /// <returns>The full path to the cached relinked .dll</returns>
        public static string GetCachedPath(EverestModuleMetadata meta, string asmname)
            => Path.Combine(Everest.Loader.PathCache, meta.Name + "." + asmname + ".dll");
        
        private static Assembly TryLoadCachedAssembly(EverestModuleMetadata meta, string asmName, string cachePath, string cacheChecksumPath, string[] checksums) {
            // Check if the cached assembly + its checksums exist on disk, and if the checksums match
            if (!File.Exists(cachePath) || !File.Exists(cacheChecksumPath)) {
                return null;
            }

            if (!Everest.ChecksumsEqual(checksums, File.ReadAllLines(cacheChecksumPath))) {
                Logger.Log(LogLevel.Verbose, "relinker", $"Cache miss - checksum mismatch for {meta} - {asmName}");
                return null;
            }

            Logger.Log(LogLevel.Verbose, "relinker", $"Loading cached assembly for {meta} - {asmName}");

            // Try to load the assembly and the module definition
            try {
                Assembly assembly = meta.AssemblyContext.LoadRelinkedAssembly(cachePath);
                Logger.Log(LogLevel.Verbose, "relinker", $"Cache hit - loaded cached assembly for {meta} - {asmName}");
                return assembly;
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "relinker", $"Cache miss - failed loading cached assembly for {meta} - {asmName}");
                e.LogDetailed();
                return null;
            }
        }

        private void RegisterCodeReloadWatcher(string asmDir) {
            // Check if there already is a watcher for the directory
            if (_AssemblyReloadWatchers.ContainsKey(asmDir))
                return;

            // Create a new watcher
            FileSystemWatcher watcher = null;
            try {
                watcher = new FileSystemWatcher(asmDir) {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                };

                watcher.Changed += (s, e) => {
                    // Check if the assembly was actually loaded
                    lock (LOCK) {
                        if (_LoadedAssemblies.GetValueOrDefault(e.FullPath.Replace('\\', '/')) == null)
                            return;
                    }

                    // Reload the assembly context
                    Logger.Log(LogLevel.Info, "modasmctx", $"Reloading mod assembly context because of changed assembly: {e.FullPath}");
                    Everest.Loader.ReloadMod(ModuleMeta);
                };
                watcher.EnableRaisingEvents = true;

                _AssemblyReloadWatchers.Add(asmDir, watcher);
                Logger.Log(LogLevel.Verbose, "modasmctx", $"Started watching assembly folder: {asmDir}");
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "modasmctx", $"Failed watching assembly folder: {asmDir}");
                e.LogDetailed();
                watcher?.Dispose();
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
                // Don't load it directly using the path so that the file on disk isn't locked
                mod = ModuleDefinition.ReadModule(path);
                if (AssemblyLoadBlackList.Contains(mod.Assembly.Name.Name, StringComparer.OrdinalIgnoreCase))
                    throw new Exception($"Attempted load of blacklisted assembly {mod.Assembly.Name} from module '{ModuleMeta.Name}'");

                Assembly asm;
                string symPath = Path.ChangeExtension(path, "pdb");
                using(Stream asmStream = File.OpenRead(path)) {
                    using(Stream symStream = File.Exists(symPath) ? File.OpenRead(symPath) : null) {
                        asm = LoadFromStream(asmStream, symStream);
                    }
                }

                // Insert into dictionaries
                string asmName = asm.GetName().Name;
                if (_AssemblyModules.TryAdd(asmName, mod)) {
                    _AssemblyLoadCache.TryAdd(asmName, asm);
                    _LocalLoadCache.TryAdd(asmName, asm);
                    _AssemblyResolveCache.TryAdd(asmName, mod.Assembly);
                } else
                    Logger.Log(LogLevel.Warn, "modasmctx", $"Assembly name conflict for name '{asmName}' for module '{ModuleMeta.Name}'!");

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

            // Try to load the assembly locally (from this or dependency ALCs)
            // If that fails, try to load the assembly globally (from non-dependency mods / game assemblies)
            Assembly asm = LoadLocal(asmName) ?? LoadGlobal(asmName);

            if (asm == null && (_ActiveLocalLoadContexts?.Count ?? 0) == 0)
                Logger.Log(LogLevel.Warn, "modasmctx", $"Failed to load assembly '{asmName.FullName}' for module '{ModuleMeta.Name}'");

            _AssemblyLoadCache.TryAdd(asmName.Name, asm);
            return asm;
        }

        protected override IntPtr LoadUnmanagedDll(string name) {
            // Lookup in the cache
            if (_AssemblyUnmanagedLoadCache.TryGetValue(name, out IntPtr cachedHandle))
                return cachedHandle;

            // Try to load the unmanaged assembly locally (from this or dependency ALCs)
            // If that fails, don't fallback to loading it globally - unmanaged dependencies have to be explicitly specified 
            IntPtr handle = LoadUnmanagedLocal(name);

            _AssemblyUnmanagedLoadCache.TryAdd(name, handle);
            return handle;
        }

        /// <summary>
        /// Resolves an assembly name reference to an assembly definition
        /// </summary>
        public AssemblyDefinition Resolve(AssemblyNameReference asmName) {
            // Lookup in the cache
            if (_AssemblyResolveCache.TryGetValue(asmName.Name, out AssemblyDefinition cachedAsm))
                return cachedAsm;

            // Try to resolve the assembly locally (from this or dependency ALCs)
            // If that fails, try to resolve the assembly globally (from non-dependency mods / game assemblies)
            AssemblyDefinition asm = ResolveLocal(asmName) ?? ResolveGlobal(asmName);

            // No warning if we failed to resolve it - the relinker will emit its own warning if needed + there's another warning upon an actual load failure

            _AssemblyResolveCache.TryAdd(asmName.Name, asm);
            return asm;
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => Resolve(name);

        private Assembly LoadLocal(AssemblyName asmName) {
            // Lookup in the cache
            if (_LocalLoadCache.TryGetValue(asmName.Name, out Assembly cachedAsm))
                return cachedAsm;

            // Try to load the assembly from this mod
            if (LoadFromThisMod(asmName) is Assembly asm) {
                _LocalLoadCache.TryAdd(asmName.Name, asm);
                return asm;
            }

            // Try to load the assembly from dependency assembly contexts
            (_ActiveLocalLoadContexts ??= new Stack<EverestModuleAssemblyContext>()).Push(this);
            try {
                foreach (EverestModuleAssemblyContext depCtx in DependencyContexts) {
                    try {
                        if (!_ActiveLocalLoadContexts.Contains(depCtx) && depCtx.LoadFromThisMod(asmName) is Assembly depAsm) {
                            _LocalLoadCache.TryAdd(asmName.Name, depAsm);
                            ActiveDependencyContexts.Add(depCtx);
                            return depAsm;
                        }
                    } catch {}
                }
            } finally {
                if (_ActiveLocalLoadContexts.Pop() != this)
                    patch_Celeste.CriticalFailureHandler(new Exception("Unexpected EverestModuleAssemblyContext on stack"));
            }

            return null;
        }

        private IntPtr LoadUnmanagedLocal(string name) {
            // Lookup in the cache
            if (_LocalUnmanagedLoadCache.TryGetValue(name, out IntPtr cachedHandle))
                return cachedHandle;

            // Try to load the assembly from this mod
            if (LoadUnmanagedFromThisMod(name) is IntPtr handle && handle != IntPtr.Zero) {
                Unloading += _ => NativeLibrary.Free(handle);
                _LocalUnmanagedLoadCache.TryAdd(name, handle);
                return handle;
            }

            // Try to load the assembly from dependency assembly contexts
            (_ActiveLocalLoadContexts ??= new Stack<EverestModuleAssemblyContext>()).Push(this);
            try {
                foreach (EverestModuleAssemblyContext depCtx in DependencyContexts) {
                    try {
                        if (!_ActiveLocalLoadContexts.Contains(depCtx) && depCtx.LoadUnmanagedDll(name) is IntPtr depHandle && depHandle != IntPtr.Zero) {
                            _LocalUnmanagedLoadCache.TryAdd(name, depHandle);
                            ActiveDependencyContexts.Add(depCtx);
                            return depHandle;
                        }
                    } catch {}
                }
            } finally {
                if (_ActiveLocalLoadContexts.Pop() != this)
                    patch_Celeste.CriticalFailureHandler(new Exception("Unexpected EverestModuleAssemblyContext on stack"));
            }

            return IntPtr.Zero;
        }

        private AssemblyDefinition ResolveLocal(AssemblyNameReference asmName) {
            // Lookup in the cache
            if (_LocalResolveCache.TryGetValue(asmName.Name, out AssemblyDefinition cachedAsm))
                return cachedAsm;

            // Try to resolve the assembly in this mod
            if (ResolveFromThisMod(asmName) is AssemblyDefinition asm) {
                _LocalResolveCache.TryAdd(asmName.Name, asm);
                return asm;
            }

            // Try to resolve the assembly in dependency assembly contexts
            (_ActiveLocalLoadContexts ??= new Stack<EverestModuleAssemblyContext>()).Push(this);
            try {
                foreach (EverestModuleAssemblyContext depCtx in DependencyContexts)
                    if (!_ActiveLocalLoadContexts.Contains(depCtx) && depCtx.ResolveFromThisMod(asmName) is AssemblyDefinition depAsm) {
                        _LocalResolveCache.TryAdd(asmName.Name, depAsm);
                        return depAsm;
                    }
            } finally {
                if (_ActiveLocalLoadContexts.Pop() != this)
                    patch_Celeste.CriticalFailureHandler(new Exception("Unexpected EverestModuleAssemblyContext on stack"));
            }

            return null;
        }

        private Assembly LoadGlobal(AssemblyName asmName) {
            // Try to load the assembly from the default assembly load context
            try {
                if (AssemblyLoadContext.Default.LoadFromAssemblyName(asmName) is Assembly globalAsm)
                    return globalAsm;
            } catch {}

            // Check if we can load this assembly from another module
            // If yes add its context as a dependency
            foreach (EverestModule module in Everest.Modules)
                if (module.Metadata.AssemblyContext?.LoadFromThisMod(asmName) is Assembly moduleAsm) {
                    Logger.Log(LogLevel.Info, "modasmctx", $"Loading assembly '{asmName.FullName}' from non-dependency '{module.Metadata.Name}' for module '{ModuleMeta.Name}'");
                    DependencyContexts.Add(module.Metadata.AssemblyContext);
                    ActiveDependencyContexts.Add(module.Metadata.AssemblyContext);
                    return moduleAsm;
                }

            return null;
        }

        private AssemblyDefinition ResolveGlobal(AssemblyNameReference asmName) {
            // Try to resolve a global assembly definition
            if (!_GlobalAssemblyResolveCache.TryGetValue(asmName.Name, out AssemblyDefinition globalAsmDef)) {
                // Try to load the global assembly
                Assembly globalAsm = null;
                try {
                    globalAsm = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(asmName.Name));
                } catch {}

                // Try to read its module
                globalAsmDef = null;
                if (!string.IsNullOrEmpty(globalAsm?.Location)) {
                    try {
                        globalAsmDef = ModuleDefinition.ReadModule(globalAsm.Location).Assembly;
                    } catch (Exception e) {
                        Logger.Log(LogLevel.Warn, "modasmctx", $"Failed to resolve global assembly definition '{asmName.FullName}'");
                        e.LogDetailed();
                    }
                }
            
                // Add to cache
                _GlobalAssemblyResolveCache.Add(asmName.Name, globalAsmDef);
            }

            // Check if we can resolve this assembly in another module
            // If yes add its context as a dependency
            _AllContextsLock.EnterReadLock();
            try {
                foreach (EverestModuleAssemblyContext alc in _AllContexts)
                    if (alc.ResolveFromThisMod(asmName) is AssemblyDefinition moduleAsm) {
                        Logger.Log(LogLevel.Info, "modasmctx", $"Resolving assembly '{asmName.FullName}' in non-dependency '{alc.ModuleMeta.Name}' for module '{ModuleMeta.Name}'");
                        DependencyContexts.Add(alc);
                        ActiveDependencyContexts.Add(alc);
                        return moduleAsm;
                    }
            } finally {
                _AllContextsLock.ExitReadLock();
            }

            return globalAsmDef;
        }

        private Assembly LoadFromThisMod(AssemblyName asmName) {
            if (_ModAsmDir == null)
                return null;

            // Try to load the assembly from the same directory as the main dll
            if (LoadAssemblyFromModPath(Path.Combine(_ModAsmDir, $"{asmName.Name}.dll")) is Assembly loadAsm)
                return loadAsm;

            return null;
        }

        private IntPtr LoadUnmanagedFromThisMod(string name) {
            if (_ModAsmDir == null)
                return IntPtr.Zero;

            // Determine the OS-specific name of the assembly
            string osName =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{name}.dll" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? $"lib{name}.so" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"lib{name}.dylib" :
                name
            ;

            // Try multiple paths to load the library from
            foreach (string libName in new[] { name, osName }) {
                if (!string.IsNullOrEmpty(ModuleMeta.PathArchive)) {
                    // We have to unzip the native libs into the cache
                    string cachePath = Path.Combine(Everest.Loader.PathCache, "unmanaged-libs", UnmanagedLibraryFolder, ModuleMeta.Name);

                    string modHash = ModuleMeta.Hash.ToHexadecimalString();

                    if (Directory.Exists(cachePath) && (!File.Exists(cachePath + ".sum") || File.ReadAllText(cachePath + ".sum") != modHash))
                        Directory.Delete(cachePath, true);

                    if (!Directory.Exists(cachePath)) {
                        // Unzip the native libs into the cache folder
                        string zipPath = Path.Combine(_ModAsmDir, UnmanagedLibraryFolder).Replace('\\', '/') + "/";
                        using (ZipFile zip = new ZipFile(ModuleMeta.PathArchive)) {
                            foreach (ZipEntry entry in zip) {
                                if (!entry.FileName.StartsWith(zipPath) || entry.IsDirectory)
                                    continue;

                                string relPath = entry.FileName.Substring(zipPath.Length).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                                if (relPath.Length <= 0)
                                    continue;

                                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(cachePath, relPath)));
                                using (Stream stream = File.Create(Path.Combine(cachePath, relPath)))
                                    entry.Extract(stream);
                            }
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                        File.WriteAllText(cachePath + ".sum", modHash);
                    }

                    // Try to load the native library from cache
                    if (NativeLibrary.TryLoad(Path.Combine(cachePath, libName), out IntPtr handle))
                        return handle;
                } else if (!string.IsNullOrEmpty(ModuleMeta.PathDirectory)) {
                    // For unzipped mods, we can simply load directly from the file system
                    if (NativeLibrary.TryLoad(Path.Combine(_ModAsmDir, UnmanagedLibraryFolder, libName), out IntPtr handle))
                        return handle;
                }
            }

            return IntPtr.Zero;
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