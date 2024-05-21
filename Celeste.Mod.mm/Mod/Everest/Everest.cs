using Celeste.Mod.Backdrops;
using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Celeste.Mod.Helpers;
using Celeste.Mod.Helpers.LegacyMonoMod;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Celeste.Mod {
    public static partial class Everest {

        /// <summary>
        /// The currently installed Everest version in string form.
        /// </summary>
        // NOTE: THIS MUST BE THE FIRST THING SET UP BY THE CLASS CONSTRUCTOR.
        // OTHERWISE OLYMPUS WON'T BE ABLE TO FIND THIS!
        // The following line gets replaced by the buildbot automatically.
        public readonly static string VersionString = "0.0.0-dev";
        /// <summary>
        /// The currently installed Everest build in string form.
        /// </summary>
        public readonly static string BuildString;

        /// <summary>
        /// The currently installed Everest version.
        /// </summary>
        public readonly static Version Version;
        /// <summary>
        /// The currently installed Everest build.
        /// </summary>
        public readonly static int Build;
        /// <summary>
        /// The currently installed Everest version suffix. For "1.2.3-a-b", this is "a-b"
        /// </summary>
        public readonly static string VersionSuffix;
        /// <summary>
        /// The currently installed Everest version tag. For "1.2.3-a-b", this is "a"
        /// </summary>
        public readonly static string VersionTag;
        /// <summary>
        /// The currently installed Everest version tag. For "1.2.3-a-b", this is "b"
        /// </summary>
        public readonly static string VersionCommit;

        /// <summary>
        /// The currently present Celeste version combined with the currently installed Everest build.
        /// </summary>
        // we cannot use Everest.Flags.IsFNA at this point because flags aren't initialized yet when it's used for the first time in log
        public static string VersionCelesteString => $"{Celeste.Instance.Version}-{(typeof(Game).Assembly.FullName.Contains("FNA") ? "fna" : "xna")} [Everest: {BuildString}]";

        /// <summary>
        /// UTF8 text encoding without a byte order mark, to be preferred over Encoding.UTF8
        /// </summary>
        public static readonly Encoding UTF8NoBOM = new UTF8Encoding(false);

        /// <summary>
        /// The command line arguments passed when launching the game.
        /// </summary>
        public static ReadOnlyCollection<string> Args { get; internal set; }

        /// <summary>
        /// A collection of all currently loaded EverestModules (mods).
        /// </summary>
        public static ReadOnlyCollection<EverestModule> Modules => _Modules.AsReadOnly();
        internal static List<EverestModule> _Modules = new List<EverestModule>();

        /// <summary>
        /// The path to the directory holding Celeste.exe
        /// </summary>
        public static string PathGame { get; internal set; }

        /// <summary>
        /// The path to the Celeste /Saves directory.
        /// </summary>
        public static string PathSettings => patch_UserIO.GetSaveFilePath();

        /// <summary>
        /// The path to Everest's log file, or null if no log file is written.
        /// </summary>
        public static string PathLog { get; internal set; }

        /// <summary>
        /// Whether XDG paths should be used.
        /// </summary>
        public static bool XDGPaths { get; internal set; }

        /// <summary>
        /// Path to Everest base location. Defaults to the game directory.
        /// </summary>
        public static string PathEverest { get; internal set; }

        /// <summary>
        /// Whether save files and settings are shared with the "restart into vanilla" install.
        /// </summary>
        public static bool ShareVanillaSaveFiles { get; internal set; }

        /// <summary>
        /// The active compatibility mode.
        /// </summary>
        public static CompatMode CompatibilityMode { get; internal set; }

        internal static bool RestartVanilla;

        internal static bool _ContentLoaded;

        /// <summary>
        /// The hasher used to determine the mod and installation hashes.
        /// </summary>
        public readonly static HashAlgorithm ChecksumHasher = XXHash64.Create();

        /// <summary>
        /// Get the checksum for a given file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>A checksum.</returns>
        public static byte[] GetChecksum(string path) {
            using (FileStream fs = File.OpenRead(path))
                return ChecksumHasher.ComputeHash(fs);
        }

        /// <summary>
        /// Get the checksum for a given mod. Might not be determined by the entire mod content.
        /// </summary>
        /// <param name="meta">The mod.</param>
        /// <returns>A checksum.</returns>
        public static byte[] GetChecksum(EverestModuleMetadata meta) {
            if (!string.IsNullOrEmpty(meta.PathArchive))
                return GetChecksum(meta.PathArchive);
            if (!string.IsNullOrEmpty(meta.DLL))
                return GetChecksum(meta.DLL);
            return new byte[0];
        }

        /// <summary>
        /// Get the checksum for a given stream.
        /// </summary>
        /// <param name="stream">A reference to the stream. Gets converted to a MemoryStream if it isn't seekable.</param>
        /// <returns>A checksum.</returns>
        public static byte[] GetChecksum(ref Stream stream) {
            if (!stream.CanSeek) {
                MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                stream.Dispose();
                stream = ms;
                stream.Seek(0, SeekOrigin.Begin);
            }

            long pos = stream.Position;
            stream.Seek(0, SeekOrigin.Begin);
            byte[] hash = ChecksumHasher.ComputeHash(stream);
            stream.Seek(pos, SeekOrigin.Begin);
            return hash;
        }
        
        /// <summary>
        /// Determine if both checksum collections are equal.
        /// </summary>
        /// <param name="a">The first checksum array.</param>
        /// <param name="b">The second checksum array.</param>
        /// <returns>True if the contents of both arrays match, false otherwise.</returns>
        public static bool ChecksumsEqual(string[] a, string[] b) {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].Trim() != b[i].Trim())
                    return false;
            return true;
        }

        private static byte[] _InstallationHash;
        public static byte[] InstallationHash {
            get {
                if (_InstallationHash != null)
                    return _InstallationHash;

                List<byte> data = new List<byte>(512);

                /*
                void AddFile(string path) {
                    using (FileStream fs = File.OpenRead(path))
                        AddStream(fs);
                }
                */
                void AddStream(Stream stream) {
                    data.AddRange(ChecksumHasher.ComputeHash(stream));
                }

                // Add all mod containers (or .DLLs).
                lock (_Modules) {
                    foreach (EverestModule mod in _Modules) {
                        if (mod?.Metadata != null)
                            data.AddRange(mod.Metadata.Hash);
                    }
                }

                // Add all map .bins
                lock (Content.Map) {
                    foreach (ModAsset asset in Content.Map.Values.ToArray()) {
                        if (asset?.Type != typeof(AssetTypeMap))
                            continue;
                        using (Stream stream = asset.Stream)
                            if (stream != null)
                                AddStream(stream);
                    }
                }

                // Return the final hash.
                return _InstallationHash = ChecksumHasher.ComputeHash(data.ToArray());
            }
        }
        public static string InstallationHashShort {
            get {
                using (HashAlgorithm hasher = XXHash64.Create()) {
                    return hasher.ComputeHash(InstallationHash).ToHexadecimalString();
                }
            }
        }
        public static void InvalidateInstallationHash() => _InstallationHash = null;

        private static bool _SavingSettings;

        private static readonly ConcurrentDictionary<Assembly, ConcurrentDictionary<object, Action>> _ModDetours = new ConcurrentDictionary<Assembly, ConcurrentDictionary<object, Action>>();
        private static readonly ConcurrentDictionary<object, Assembly> _DetourOwners = new ConcurrentDictionary<object, Assembly>();
        internal static List<string> _DetourLog = new List<string>();

        public static readonly float SystemMemoryMB;

        static Everest() {
            int versionSplitIndex = VersionString.IndexOf('-');
            if (versionSplitIndex == -1) {
                Version = new Version(VersionString);
                Build = Version.Minor;
                VersionSuffix = "";
                VersionTag = "";
                VersionCommit = "";
                BuildString = Build.ToString();

            } else {
                Version = new Version(VersionString.Substring(0, versionSplitIndex));
                Build = Version.Minor;
                VersionSuffix = VersionString.Substring(versionSplitIndex + 1);
                BuildString = Version.Minor + "-" + VersionSuffix;
                versionSplitIndex = VersionSuffix.IndexOf('-');
                if (versionSplitIndex == -1) {
                    VersionTag = VersionSuffix;
                    VersionCommit = "";
                } else {
                    VersionTag = VersionSuffix.Substring(0, versionSplitIndex);
                    VersionCommit = VersionSuffix.Substring(versionSplitIndex + 1);
                }
            }

            SystemMemoryMB = (float) GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024;
        }

        internal static void ParseArgs(string[] args) {
            // Expose the arguments to all other mods in a read-only collection.
            Args = new ReadOnlyCollection<string>(args);

            Queue<string> queue = new Queue<string>(args);
            while (queue.Count > 0) {
                string arg = queue.Dequeue();

                if (arg == "--debug")
                    Celeste.PlayMode = Celeste.PlayModes.Debug;

                else if (arg == "--debugger")
                    Debugger.Launch();
                    
                else if (arg == "--debugger-attach") {
                    Logger.Log(LogLevel.Info, "Everest", "Waiting for debugger to attach...");
                    while (!Debugger.IsAttached) { }
                    Logger.Log(LogLevel.Info, "Everest", "Debugger attached");
                }

                else if (arg == "--dump")
                    Content.DumpOnLoad = true;
                else if (arg == "--dump-all")
                    Content._DumpAll = true;

                else if (arg == "--headless")
                    Environment.SetEnvironmentVariable("EVEREST_HEADLESS", "1");

                else if (arg == "--everest-disabled")
                    Environment.SetEnvironmentVariable("EVEREST_DISABLED", "1");

                else if (arg == "--whitelist" && queue.Count >= 1)
                    Loader.NameWhitelist = queue.Dequeue();

                else if (arg == "--blacklist" && queue.Count >= 1)
                    Loader.NameTemporaryBlacklist = queue.Dequeue();

                else if (arg == "--loglevel" && queue.Count >= 1) {
                    if (Enum.TryParse(queue.Dequeue(), ignoreCase: true, out LogLevel level))
                        Logger.SetLogLevelFromSettings("", level);
                }
                
                else if (arg == "--use-scancodes") {
                    Environment.SetEnvironmentVariable("FNA_KEYBOARD_USE_SCANCODES", "1");
                }
            }
        }

        internal static void Boot() {
            Logger.Log(LogLevel.Info, "core", "Booting Everest");
            Logger.Log(LogLevel.Info, "core", $"AppDomain: {AppDomain.CurrentDomain.FriendlyName ?? "???"}");
            Logger.Log(LogLevel.Info, "core", $"VersionCelesteString: {VersionCelesteString}");
            Logger.Log(LogLevel.Info, "core", $"SystemMemoryMB: {SystemMemoryMB:F3} MB");

            if (Type.GetType("Mono.Runtime") != null) {
                // Mono hates HTTPS.
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => {
                    return true;
                };
            }

            // enable TLS 1.2 to fix connecting to everestapi.github.io
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            PathGame = Path.GetDirectoryName(typeof(Celeste).Assembly.Location);

            // .NET hates it when strong-named dependencies get updated.
            AppDomain.CurrentDomain.AssemblyResolve += (asmSender, asmArgs) => {
                AssemblyName asmName = new AssemblyName(asmArgs.Name);
                if (!asmName.Name.StartsWith("Mono.Cecil") &&
                    !asmName.Name.StartsWith("YamlDotNet") &&
                    !asmName.Name.StartsWith("NLua") &&
                    !asmName.Name.StartsWith("DotNetZip") &&
                    !asmName.Name.StartsWith("Newtonsoft.Json") &&
                    !asmName.Name.StartsWith("Jdenticon"))
                    return null;

                Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(other => other.GetName().Name == asmName.Name);
                if (asm != null)
                    return asm;

                return Assembly.LoadFrom(Path.Combine(PathGame, asmName.Name + ".dll"));
            };

            // Preload some basic dependencies.
            Assembly.Load("MonoMod.RuntimeDetour");
            Assembly.Load("MonoMod.Utils");
            Assembly.Load("Mono.Cecil");
            Assembly.Load("YamlDotNet");
            Assembly.Load("Newtonsoft.Json");
            Assembly.Load("Jdenticon");

            if (!File.Exists(Path.Combine(PathGame, "EverestXDGFlag"))) {
                XDGPaths = false;
                PathEverest = PathGame;
            } else {
                XDGPaths = true;
                string dataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                Directory.CreateDirectory(PathEverest = Path.Combine(dataDir, "Everest"));
                Directory.CreateDirectory(Path.Combine(dataDir, "Everest", "Mods")); // Make sure it exists before content gets initialized
            }

            // Old versions of Everest have used a separate ModSettings folder.
            string modSettingsOld = Path.Combine(PathEverest, "ModSettings");
            string modSettingsRIP = Path.Combine(PathEverest, "ModSettings-OBSOLETE");
            if (Directory.Exists(modSettingsOld) || Directory.Exists(modSettingsRIP)) {
                Logger.Log(LogLevel.Warn, "core", "THE ModSettings FOLDER IS OBSOLETE AND WILL NO LONGER BE USED!");
                if (Directory.Exists(modSettingsOld) && !Directory.Exists(modSettingsRIP))
                    Directory.Move(modSettingsOld, modSettingsRIP);
            }

            static void RegisterModDetour(Assembly owner, object detour, Action undo) {
                _ModDetours.GetOrAdd(owner, _ => new ConcurrentDictionary<object, Action>()).TryAdd(detour, undo);
                _DetourOwners.TryAdd(detour, owner);
            }

            static void UnregisterModDetour(object detour) {
                if (!_DetourOwners.TryRemove(detour, out Assembly owner))
                    return;

                if (_ModDetours.TryGetValue(owner, out ConcurrentDictionary<object, Action> detours))
                    detours.TryRemove(detour, out _);
            }

            DetourManager.DetourApplied += info => {
                if (GetHookOwner(out bool isMMHook) is not Assembly owner)
                    return;

                if (!isMMHook)
                    _DetourLog.Add($"new Detour by {owner.GetName().Name}: {info.Method.Method.GetID()}");
                else
                    _DetourLog.Add($"new On.+= by {owner.GetName().Name}: {info.Method.Method.GetID()}");

                RegisterModDetour(owner, info, info.Undo);
            };
            DetourManager.DetourUndone += UnregisterModDetour;

            DetourManager.ILHookApplied += info => {
                if (GetHookOwner(out bool isMMHook) is not Assembly owner)
                    return;

                if (!isMMHook)
                    _DetourLog.Add($"new ILHook by {owner.GetName().Name}: {info.Method.Method.GetID()}");
                else
                    _DetourLog.Add($"new IL.+= by {owner.GetName().Name}: {info.Method.Method.GetID()}");

                RegisterModDetour(owner, info, info.Undo);
            };
            DetourManager.ILHookUndone += UnregisterModDetour;
            
            DetourManager.NativeDetourApplied += info => {
                if (GetHookOwner(out _) is not Assembly owner)
                    return;

                _DetourLog.Add($"new NativeDetour by {owner.GetName().Name}: {info.Function.Function:X16}");

                RegisterModDetour(owner, info, info.Undo);
            };
            DetourManager.NativeDetourUndone += UnregisterModDetour;

            LegacyMonoModCompatLayer.Initialize();

            // Before even initializing anything else, make sure to prepare any static flags.
            Flags.Initialize();

            if (!Flags.IsHeadless) {
                // Initialize the content helper.
                Content.Initialize();
            }

            MainThreadHelper.Instance = new MainThreadHelper(Celeste.Instance);
            STAThreadHelper.Instance = new STAThreadHelper(Celeste.Instance);

            // Register our core module and load any other modules.
            CoreModule core = new CoreModule();
            core.Register();
            Assembly asm = typeof(CoreModule).Assembly;
            Type[] types = asm.GetTypesSafe();
            Loader.ProcessAssembly(core.Metadata, asm, types);
            core.Metadata.RegisterMod();

            // Note: Everest fulfills some mod dependencies by itself.
            NullModule vanilla = new NullModule(new EverestModuleMetadata {
                Name = "Celeste",
                VersionString = $"{Celeste.Instance.Version.ToString()}-{(Flags.IsFNA ? "fna" : "xna")}"
            });
            vanilla.Register();
            vanilla.Metadata.RegisterMod();

            LuaLoader.Initialize();

            Loader.LoadAuto();

            if (!Flags.IsHeadless) {
                // Load stray .bins afterwards.
                Content.Crawl(new MapBinsInModsModContent(Path.Combine(PathEverest, "Mods")));
            }

            // Also let all mods parse the arguments.
            Queue<string> args = new Queue<string>(Args);
            while (args.Count > 0) {
                string arg = args.Dequeue();
                foreach (EverestModule mod in _Modules) {
                    if (mod.ParseArg(arg, args))
                        break;
                }
            }

            // Start requesting the version list ASAP.
            Updater._VersionListRequestTask = Updater.RequestAll();

            // Check if an update failed
            Updater.CheckForUpdateFailure();

            // Request the mod update list as well.
            ModUpdaterHelper.RunAsyncCheckForModUpdates(excludeBlacklist: true);

            // Cleanup mod ALCs
            EverestModuleAssemblyContext._AllContextsLock.EnterReadLock();
            try {
                foreach (EverestModuleAssemblyContext alc in EverestModuleAssemblyContext._AllContexts)
                    alc.PostBootCleanup();
            } finally {
                EverestModuleAssemblyContext._AllContextsLock.ExitReadLock();
            }

            DiscordSDK.LoadRichPresenceIcons();
        }

        internal static bool _Initialized;
        internal static void Initialize() {
            // Initialize misc stuff.
            if (Content._DumpAll)
                Content.DumpAll();

            TextInput.Initialize(Celeste.Instance);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                DirectoryInfo vanillaSavesDir = new DirectoryInfo(Path.Combine(PathGame, "orig", "Saves"));
                ShareVanillaSaveFiles = vanillaSavesDir.Exists && vanillaSavesDir.LinkTarget != null;
            } else
                ShareVanillaSaveFiles = true;

            AutoSplitter.Init();

            // Add the previously created managers.
            Celeste.Instance.Components.Add(MainThreadHelper.Instance);
            Celeste.Instance.Components.Add(STAThreadHelper.Instance);

            foreach (EverestModule mod in _Modules)
                mod.Initialize();
            _Initialized = true;

            DecalRegistry.LoadDecalRegistry();


            Celeste.Instance.Disposed += Dispose;
        }

        internal static void Shutdown() {
            DebugRC.Shutdown();
            TextInput.Shutdown();
            AutoSplitter.Shutdown();
            Events.Celeste.Shutdown();
            LegacyMonoModCompatLayer.Uninitialize();
        }

        internal static void Dispose(object sender, EventArgs args) {
            Audio.Unload(); // This exists but never gets called by the vanilla game.
            
            foreach (ConcurrentDictionary<object, Action> detours in _ModDetours.Values)
                foreach (Action detourUndo in detours.Values)
                    detourUndo();
            _ModDetours.Clear();
        }

        /// <summary>
        /// Register a new EverestModule (mod) dynamically. Invokes LoadSettings and Load.
        /// </summary>
        /// <param name="module">Mod to register.</param>
        public static void Register(this EverestModule module) {
            lock (_Modules)
                _Modules.Add(module);

            module.LoadSettings();
            module.Load();
            if (_ContentLoaded) {
                module.LoadContent(true);
            }
            if (_Initialized) {
                if (_ModInitBatch != null)
                    _ModInitBatch.LateInitializeModule(module);
                else
                    LateInitializeModules(Enumerable.Repeat(module, 1));
            }

            module.LogRegistration();
            Events.Everest.RegisterModule(module);
        }

        internal static void LateInitializeModules(IEnumerable<EverestModule> modules) {
            // Re-initialize the tracker
            Tracker.Initialize();

            // Initialize mods
            foreach (EverestModule module in modules)
                module.Initialize();

            // Re-initialize inputs + reload commands
            Input.Initialize();
            ((Monocle.patch_Commands) Engine.Commands).ReloadCommandsList();

            // If we are in a save, load save data
            if (SaveData.Instance != null) {
                foreach (EverestModule module in modules) {
                    // we are in a save. we are expecting the save data to already be loaded at this point
                    Logger.Log(LogLevel.Verbose, "core", $"Loading save data slot {SaveData.Instance.FileSlot} for {module.Metadata}");
                    if (module.SaveDataAsync) {
                        module.DeserializeSaveData(SaveData.Instance.FileSlot, module.ReadSaveData(SaveData.Instance.FileSlot));
                    } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                        module.LoadSaveData(SaveData.Instance.FileSlot);
#pragma warning restore CS0618
                    }

                    if (SaveData.Instance.CurrentSession?.InArea ?? false) {
                        // we are in a level. we are expecting the session to already be loaded at this point
                        Logger.Log(LogLevel.Verbose, "core", $"Loading session slot {SaveData.Instance.FileSlot} for {module.Metadata}");
                        if (module.SaveDataAsync) {
                            module.DeserializeSession(SaveData.Instance.FileSlot, module.ReadSession(SaveData.Instance.FileSlot));
                        } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                            module.LoadSession(SaveData.Instance.FileSlot, false);
#pragma warning restore CS0618
                        }
                    }
                }
            }

            // Check if any module defines a PrepareMapDataProcessors method. If this is the case, we want to reload maps so that they are applied.
            foreach (EverestModule module in modules) {
                if (module.GetType().GetMethod("PrepareMapDataProcessors", new Type[] { typeof(MapDataFixup) })?.DeclaringType == module.GetType()) {
                    Logger.Log(LogLevel.Verbose, "core", $"Module {module.Metadata} has map data processors: reloading maps.");
                    TriggerModInitMapReload();
                    break;
                }
            }
        }

        [ThreadStatic]
        private static ModInitializationBatch _ModInitBatch;

        internal class ModInitializationBatch : IDisposable {
            public bool IsActive { get; private set; }

            private bool shouldReloadMaps = false;
            private Queue<EverestModule> lateModuleInitQueue = new Queue<EverestModule>();

            public ModInitializationBatch() {
                if (_ModInitBatch != null)
                    return;

                IsActive = true;
                _ModInitBatch = this;
            }

            public void Dispose() {
                if (!IsActive)
                    return;
                Trace.Assert(_ModInitBatch == this);

                // Flush the batch
                Flush();

                // Reset the mod init batch
                _ModInitBatch = null;
                IsActive = false;
            }

            public void ReloadMaps() {
                if (!IsActive)
                    throw new InvalidOperationException("Can't add tasks to an inactive mod initialization batch");
                shouldReloadMaps = true;
            }

            public void LateInitializeModule(EverestModule module) {
                if (!IsActive)
                    throw new InvalidOperationException("Can't add tasks to an inactive mod initialization batch");
                lateModuleInitQueue.Enqueue(module);
            }

            public void Flush() {
                if (!IsActive)
                    return;

                // Flush the module late-initialization queue
                // This might set shouldReloadMaps
                if (lateModuleInitQueue.Count > 0) {
                    LateInitializeModules(lateModuleInitQueue);
                    lateModuleInitQueue.Clear();
                }

                // Reload maps if we should
                if (shouldReloadMaps) {
                    AssetReloadHelper.ReloadAllMaps();
                    shouldReloadMaps = false;
                }
            }
        }

        internal static void TriggerModInitMapReload() {
            if (_ModInitBatch != null)
                _ModInitBatch.ReloadMaps();
            else
                AssetReloadHelper.ReloadAllMaps();
        }

        internal static void CheckDependenciesOfDelayedMods() {
            // Attempt to load mods after their dependencies have been loaded.
            // Only load and lock the delayed list if we're not already loading delayed mods.
            if (Interlocked.CompareExchange(ref Loader.DelayedLock, 1, 0) == 0) {
                try {
                    lock (Loader.Delayed) {
                        bool enforceTransitiveOptionalDependencies = true;
                        bool didDelayOptionalDependencies = false;

                        for (int i = 0; i < Loader.Delayed.Count; i++) {
                            Tuple<EverestModuleMetadata, Action> entry = Loader.Delayed[i];

                            if (Loader.DependenciesLoaded(entry.Item1)) {
                                if (enforceTransitiveOptionalDependencies
                                    && checkIfOneOfDependenciesIsDelayedAndCanBeLoaded(entry.Item1.OptionalDependencies, new HashSet<EverestModuleMetadata>() { entry.Item1 })) {

                                    // the mod has all its dependencies loaded, but one of its optional dependencies is not loaded and can be loaded as well.
                                    // so we should wait for it to be loaded first.
                                    didDelayOptionalDependencies = true;
                                } else {
                                    // all dependencies are loaded, all optional dependencies are either loaded or won't load => we're good to go!
                                    Logger.Log(LogLevel.Info, "core", $"Dependencies of mod {entry.Item1} are now satisfied: loading");
                                    EverestSplashHandler.IncreaseLoadedModCount(entry.Item1.Name); // Notify the splash
                                    
                                    if (Everest.Modules.Any(mod => mod.Metadata.Name == entry.Item1.Name)) {
                                        // a duplicate of the mod was loaded while it was sitting in the delayed list.
                                        Logger.Log(LogLevel.Warn, "core", $"Mod {entry.Item1.Name} already loaded!");
                                    } else {
                                        // actually load the mod.
                                        entry.Item2?.Invoke();
                                        Loader.LoadMod(entry.Item1);
                                    }
                                    Loader.Delayed.RemoveAt(i);

                                    // we now loaded an extra mod, consider all delayed mods again to deal with transitive dependencies.
                                    i = -1;
                                    didDelayOptionalDependencies = false;
                                }
                            }

                            if (i == Loader.Delayed.Count - 1 && didDelayOptionalDependencies) {
                                // we considered all mods, but we delayed some of them because they had an optional dependency that "could be loaded"... yet nothing got loaded.
                                // that's probably a circular optional dependency case, so we should just reconsider everything again ignoring optional dependencies.
                                Logger.Log(LogLevel.Warn, "core", $"Mods with unsatisfied optional dependencies were delayed but never loaded (probably due to circular optional dependencies), forcing them to load!");
                                enforceTransitiveOptionalDependencies = false;

                                i = -1;
                                didDelayOptionalDependencies = false;
                            }
                        }
                    }
                } finally {
                    Interlocked.Decrement(ref Loader.DelayedLock);
                }
            }
        }

        // This method checks if one of the dependencies given is in Loader.Delayed and has its own dependencies satisfied (so it can be loaded).
        // If not, it also checks if those dependencies are themselves in Loader.Delayed and can be loaded (using alreadyChecked to prevent infinite loops in case of circular dependencies).
        private static bool checkIfOneOfDependenciesIsDelayedAndCanBeLoaded(List<EverestModuleMetadata> dependencies, HashSet<EverestModuleMetadata> alreadyChecked) {
            foreach (EverestModuleMetadata dependency in dependencies) {
                // only consider dependencies we didn't consider yet, and ones that aren't loaded yet.
                if (!Loader.DependencyLoaded(dependency) && !alreadyChecked.Contains(dependency)) {
                    EverestModuleMetadata delayed = Loader.Delayed.FirstOrDefault(item => item.Item1.Name == dependency.Name)?.Item1;
                    if (delayed != null && !alreadyChecked.Contains(delayed)) {
                        if (Loader.DependenciesLoaded(delayed)) {
                            // this dependency is going to be loaded later!
                            return true;
                        } else {
                            alreadyChecked.Add(delayed);
                            if (checkIfOneOfDependenciesIsDelayedAndCanBeLoaded(delayed.Dependencies, alreadyChecked)
                                || checkIfOneOfDependenciesIsDelayedAndCanBeLoaded(delayed.OptionalDependencies, alreadyChecked)) {

                                // one of the transitive dependencies can be loaded: delay just in case.
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Unregisters an already registered EverestModule (mod) dynamically. Invokes Unload.
        /// </summary>
        /// <param name="module"></param>
        internal static void Unregister(this EverestModule module) {
            module.OnInputDeregister();
            module.Unload();
    
            // TODO: Unload from LuaLoader
            // TODO: Unload from EntityLoaders
            // TODO: Undo event listeners
            // TODO: Unload from registries
            // TODO: Make sure modules depending on this are unloaded as well.
            // TODO: Unload content, textures, audio, maps, AAAAAAAAAAAAAAAAAAAAAAA

            lock (_Modules)
                _Modules.RemoveAt(_Modules.IndexOf(module));

            if (_Initialized) {
                ((Monocle.patch_Commands) Engine.Commands).ReloadCommandsList();
            }

            InvalidateInstallationHash();

            module.LogUnregistration();
        }

        /// <summary>
        /// "Unloads" an assembly. This ensures that no references to the modules are kept alive
        /// </summary>
        /// <param name="meta"></param>
        /// <param name="asm"></param>
        internal static void UnloadAssembly(EverestModuleMetadata meta, Assembly asm) {
            // Unregister all modules contained in the assembly
            EverestModule[] asmModules;
            lock (_Modules)
                asmModules = _Modules.Where(m => m.GetType().Assembly == asm).ToArray();

            foreach (EverestModule mod in asmModules)
                Unregister(mod);

            // Remove hooks
            if (_ModDetours.TryRemove(asm, out ConcurrentDictionary<object, Action> detours))
                foreach (Action detourUndo in detours.Values)
                    detourUndo();
        }

        /// <summary>
        /// Save all mod and user settings. Use this instead of UserIO.SaveHandler(false, true)
        /// </summary>
        /// <returns>The routine enumerator.</returns>
        public static IEnumerator SaveSettings() {
            if (_SavingSettings)
                return _SaveNoSettings();
            _SavingSettings = true;
            // This is needed because the entity holding the routine could be removed,
            // leaving this in a "hanging" state.
            return new SafeRoutine(_SaveSettings());
        }

        private static IEnumerator _SaveSettings() {
            bool saving = true;
            RunThread.Start(() => {
                foreach (EverestModule mod in _Modules)
                    mod.SaveSettings();
                saving = false;
            }, "MOD_IO", false);

            SaveLoadIcon.Show(Engine.Scene);
            while (saving)
                yield return null;
            yield return patch_UserIO.SaveHandlerLegacy(false, true);
            SaveLoadIcon.Hide();

            _SavingSettings = false;
        }

        private static IEnumerator _SaveNoSettings() {
            yield break;
        }

        public static void QuickFullRestart() {
            if (AppDomain.CurrentDomain.IsDefaultAppDomain() || !CoreModule.Settings.RestartAppDomain_WIP) {
                SlowFullRestart();
                return;
            }

            BlackScreen scene = new BlackScreen();
            scene.HelperEntity.Add(new Coroutine(_QuickFullRestart(Engine.Scene is Overworld, scene)));
            Engine.Scene = scene;
        }

        private static IEnumerator _QuickFullRestart(bool fromOverworld, BlackScreen scene) {
            SaveData save = SaveData.Instance;
            if (save != null && save.FileSlot == patch_SaveData.LoadedModSaveDataIndex) {
                if (!fromOverworld) {
                    CoreModule.Settings.QuickRestart = save.FileSlot;
                }
                save.BeforeSave();
                UserIO.Save<SaveData>(SaveData.GetFilename(save.FileSlot), UserIO.Serialize(save));
                patch_UserIO.ForceSerializeModSave();
                if (CoreModule.Settings.SaveDataFlush ?? false)
                    CoreModule.Instance.ForceSaveDataFlush++;
                CoreModule.Instance.SaveSettings();
            }

            AppDomain.CurrentDomain.SetData("EverestRestart", true);
            scene.RunAfterRender = () => {
                if (Flags.IsXNA && Engine.Graphics.IsFullScreen) {
                    Engine.SetWindowed(320 * (Settings.Instance?.WindowScale ?? 1), 180 * (Settings.Instance?.WindowScale ?? 1));
                }
                Engine.Instance.Exit();
            };
            yield break;
        }

        public static void SlowFullRestart() {
            BlackScreen scene = new BlackScreen();
            scene.HelperEntity.Add(new Coroutine(_SlowFullRestart(Engine.Scene is Overworld, scene)));
            Engine.Scene = scene;
        }

        private static IEnumerator _SlowFullRestart(bool fromOverworld, BlackScreen scene) {
            SaveData save = SaveData.Instance;
            if (save != null && save.FileSlot == patch_SaveData.LoadedModSaveDataIndex) {
                if (!fromOverworld) {
                    CoreModule.Settings.QuickRestart = save.FileSlot;
                }
                save.BeforeSave();
                UserIO.Save<SaveData>(SaveData.GetFilename(save.FileSlot), UserIO.Serialize(save));
                patch_UserIO.ForceSerializeModSave();
                if (CoreModule.Settings.SaveDataFlush ?? false)
                    CoreModule.Instance.ForceSaveDataFlush++;
                CoreModule.Instance.SaveSettings();
            }

            Events.Celeste.OnShutdown += static () => BOOT.StartCelesteProcess();
            scene.RunAfterRender = () => Engine.Instance.Exit();
            yield break;
        }

        internal static Assembly GetHookOwner(out bool isMMHOOK, StackTrace stack = null) {
            isMMHOOK = false;

            // Stack walking is not fast, but it's the only option
            if (stack == null)
                stack = new StackTrace();

            int frameCount = stack.FrameCount;
            for (int i = 0; i < frameCount; i++) {
                StackFrame frame = stack.GetFrame(i);
                MethodBase caller = frame.GetMethod();

                Assembly asm = caller?.DeclaringType?.Assembly;
                if (asm == null)
                    continue;

                if (asm == Assembly.GetExecutingAssembly())
                    continue;

                if (asm == typeof(Hook).Assembly)
                    continue;

                if (asm.GetName().Name.StartsWith("MMHOOK_")) {
                    isMMHOOK = true;
                    continue;
                }

                return asm;
            }

            return null;
        }

        public static void LogDetours(LogLevel level = LogLevel.Debug) {
            List<string> detours = _DetourLog;
            if (detours.Count == 0)
                return;

            _DetourLog = new List<string>();

            foreach (string line in detours)
                Logger.Log(level, "detours", line);
        }

        [Obsolete("Use Type.EmptyTypes instead")]
        public readonly static Type[] _EmptyTypeArray = new Type[0];
        [Obsolete("Use Array.Empty<object> instead")]
        public readonly static object[] _EmptyObjectArray = new object[0];

        public enum CompatMode {
            None,
            LegacyXNA, // 61 FPS jank
            LegacyFNA  // Artificial input latency
        }

    }
}
