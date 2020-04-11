using Celeste.Mod.Core;
using Monocle;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using System.Globalization;
using System.Security.Cryptography;
using YYProject.XXHash;
using Celeste.Mod.Entities;
using Celeste.Mod.Helpers;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;

namespace Celeste.Mod {
    public static partial class Everest {

        /// <summary>
        /// The currently installed Everest version in string form.
        /// </summary>
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
        public static string VersionCelesteString => $"{Celeste.Instance.Version} [Everest: {BuildString}]";

        /// <summary>
        /// The command line arguments passed when launching the game.
        /// </summary>
        public static ReadOnlyCollection<string> Args { get; internal set; }

        /// <summary>
        /// A collection of all currently loaded EverestModules (mods).
        /// </summary>
        public static ReadOnlyCollection<EverestModule> Modules => _Modules.AsReadOnly();
        internal static List<EverestModule> _Modules = new List<EverestModule>();
        private static List<Assembly> _RelinkedAssemblies = new List<Assembly>();

        /// <summary>
        /// The path to the directory holding Celeste.exe
        /// </summary>
        public static string PathGame { get; internal set; }

        /// <summary>
        /// The path to the Celeste /Saves directory.
        /// </summary>
        public static string PathSettings => patch_UserIO.GetSaveFilePath();
        /// <summary>
        /// Whether XDG paths should be used.
        /// </summary>
        public static bool XDGPaths { get; internal set; }
        /// <summary>
        /// Path to Everest base location. Defaults to the game directory.
        /// </summary>
        public static string PathEverest { get; internal set; }

        internal static bool RestartVanilla;

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

        private static DetourModManager _DetourModManager;
        private static HashSet<Assembly> _DetourOwners = new HashSet<Assembly>();
        internal static List<string> _DetourLog = new List<string>();

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

            }
        }

        internal static void Boot() {
            Logger.Log(LogLevel.Info, "core", "Booting Everest");
            Logger.Log(LogLevel.Info, "core", $"AppDomain: {AppDomain.CurrentDomain.FriendlyName ?? "???"}");
            Logger.Log(LogLevel.Info, "core", $"VersionCelesteString: {VersionCelesteString}");

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
                    !asmName.Name.StartsWith("NLua"))
                    return null;

                Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(other => other.GetName().Name == asmName.Name);
                if (asm != null)
                    return asm;

                return Assembly.LoadFrom(Path.Combine(PathGame, asmName.Name + ".dll"));
            };

            // .NET hates to acknowledge manually loaded assemblies.
            AppDomain.CurrentDomain.AssemblyResolve += (asmSender, asmArgs) => {
                AssemblyName asmName = new AssemblyName(asmArgs.Name);
                foreach (Assembly asm in _RelinkedAssemblies) {
                    if (asm.GetName().Name == asmName.Name)
                        return asm;
                }

                return null;
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
                var dataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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

            _DetourModManager = new DetourModManager();
            _DetourModManager.OnILHook += (owner, from, to) => {
                _DetourOwners.Add(owner);
                object target = to.Target;
                _DetourLog.Add($"new ILHook by {owner.GetName().Name}: {from.GetID()} -> {to.Method?.GetID() ?? "???"}" + (target == null ? "" : $" (target: {target})"));
            };
            _DetourModManager.OnHook += (owner, from, to, target) => {
                _DetourOwners.Add(owner);
                _DetourLog.Add($"new Hook by {owner.GetName().Name}: {from.GetID()} -> {to.GetID()}" + (target == null ? "" : $" (target: {target})"));
            };
            _DetourModManager.OnDetour += (owner, from, to) => {
                _DetourOwners.Add(owner);
                _DetourLog.Add($"new Detour by {owner.GetName().Name}: {from.GetID()} -> {to.GetID()}");
            };
            _DetourModManager.OnNativeDetour += (owner, fromMethod, from, to) => {
                _DetourOwners.Add(owner);
                _DetourLog.Add($"new NativeDetour by {owner.GetName().Name}: {fromMethod?.ToString() ?? from.ToString("16X")} -> {to.ToString("16X")}");
            };
            HookEndpointManager.OnAdd += (from, to) => {
                Assembly owner = HookEndpointManager.GetOwner(to) as Assembly ?? typeof(Everest).Assembly;
                _DetourOwners.Add(owner);
                object target = to.Target;
                _DetourLog.Add($"new On.+= by {owner.GetName().Name}: {from.GetID()} -> {to.Method?.GetID() ?? "???"}" + (target == null ? "" : $" (target: {target})"));
                return true;
            };
            HookEndpointManager.OnModify += (from, to) => {
                Assembly owner = HookEndpointManager.GetOwner(to) as Assembly ?? typeof(Everest).Assembly;
                _DetourOwners.Add(owner);
                object target = to.Target;
                _DetourLog.Add($"new IL.+= by {owner.GetName().Name}: {from.GetID()} -> {to.Method?.GetID() ?? "???"}" + (target == null ? "" : $" (target: {target})"));
                return true;
            };

            // Before even initializing anything else, make sure to prepare any static flags.
            Flags.Initialize();

            if (!Flags.IsDisabled && !Flags.IsDisabled) {
                // 0.1 parses into 1 in regions using ,
                // This also somehow sets the exception message language to English.
                CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            }

            if (!Flags.IsHeadless) {
                // Initialize the content helper.
                Content.Initialize();

                // Initialize all main managers before loading any mods.
                TouchInputManager.Instance = new TouchInputManager(Celeste.Instance);
                // Don't add it yet, though - add it in Initialize.
            }

            MainThreadHelper.Instance = new MainThreadHelper(Celeste.Instance);

            // Register our core module and load any other modules.
            new CoreModule().Register();

            // Note: Everest fulfills some mod dependencies by itself.
            new NullModule(new EverestModuleMetadata() {
                Name = "Celeste",
                VersionString = $"{Celeste.Instance.Version.ToString()}-{(typeof(Game).Assembly.FullName.Contains("FNA") ? "fna" : "xna")}"
            }).Register();
            new NullModule(new EverestModuleMetadata() {
                Name = "DialogCutscene",
                VersionString = "1.0.0"
            }).Register();
            new NullModule(new EverestModuleMetadata() {
                Name = "UpdateChecker",
                VersionString = "1.0.2"
            }).Register();

            LuaLoader.Initialize();

            Loader.LoadAuto();

            if (!Flags.IsHeadless && !Flags.IsDisabled) {
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
            Updater.RequestAll();

            if (CoreModule.Settings.AutoUpdateModsOnStartup) {
                // Request the mod update list as well.
                ModUpdaterHelper.RunAsyncCheckForModUpdates();
            }
        }

        internal static bool _Initialized;
        internal static void Initialize() {
            // Initialize misc stuff.
            TextInput.Initialize(Celeste.Instance);
            if (!Flags.IsDisabled) {
                Discord.Initialize();
            }

            // Add the previously created managers.
            if (TouchInputManager.Instance != null)
                Celeste.Instance.Components.Add(TouchInputManager.Instance);
            Celeste.Instance.Components.Add(MainThreadHelper.Instance);

            foreach (EverestModule mod in _Modules)
                mod.Initialize();
            _Initialized = true;

            DecalRegistry.LoadDecalRegistry();

            // If anyone's still using the relinker past this point, at least make sure that it won't grow endlessly.
            Relinker.Modder.Dispose();
            Relinker.Modder = null;
            Relinker.SharedModder = false;

            Celeste.Instance.Disposed += Dispose;
        }

        internal static void Shutdown() {
            DebugRC.Shutdown();
            Events.Celeste.Shutdown();
        }

        internal static void Dispose(object sender, EventArgs args) {
            Audio.Unload(); // This exists but never gets called by the vanilla game.

            if (_DetourModManager != null) {
                foreach (Assembly asm in _DetourOwners)
                    _DetourModManager.Unload(asm);

                _DetourModManager.Dispose();
                _DetourModManager = null;
                _DetourOwners.Clear();
            }
        }

        /// <summary>
        /// Register a new EverestModule (mod) dynamically. Invokes LoadSettings and Load.
        /// </summary>
        /// <param name="module">Mod to register.</param>
        public static void Register(this EverestModule module) {
            lock (_Modules) {
                _Modules.Add(module);
            }

            LuaLoader.Precache(module.GetType().Assembly);

            foreach (Type type in module.GetType().Assembly.GetTypes()) {
                // Search for all entities marked with the CustomEntityAttribute.
                foreach (CustomEntityAttribute attrib in type.GetCustomAttributes<CustomEntityAttribute>()) {
                    foreach (string idFull in attrib.IDs) {
                        string id;
                        string genName;
                        string[] split = idFull.Split('=');

                        if (split.Length == 1) {
                            id = split[0];
                            genName = "Load";

                        } else if (split.Length == 2) {
                            id = split[0];
                            genName = split[1];

                        } else {
                            Logger.Log(LogLevel.Warn, "core", $"Invalid number of custom entity ID elements: {idFull} ({type.FullName})");
                            continue;
                        }

                        id = id.Trim();
                        genName = genName.Trim();

                        patch_Level.EntityLoader loader = null;

                        ConstructorInfo ctor;
                        MethodInfo gen;

                        gen = type.GetMethod(genName, new Type[] { typeof(Level), typeof(LevelData), typeof(Vector2), typeof(EntityData) });
                        if (gen != null && gen.IsStatic && gen.ReturnType.IsCompatible(typeof(Entity))) {
                            loader = (level, levelData, offset, entityData) => (Entity) gen.Invoke(null, new object[] { level, levelData, offset, entityData });
                            goto RegisterEntityLoader;
                        }

                        ctor = type.GetConstructor(new Type[] { typeof(EntityData), typeof(Vector2), typeof(EntityID) });
                        if (ctor != null) {
                            loader = (level, levelData, offset, entityData) => (Entity) ctor.Invoke(new object[] { entityData, offset, new EntityID(levelData.Name, entityData.ID) });
                            goto RegisterEntityLoader;
                        }

                        ctor = type.GetConstructor(new Type[] { typeof(EntityData), typeof(Vector2) });
                        if (ctor != null) {
                            loader = (level, levelData, offset, entityData) => (Entity) ctor.Invoke(new object[] { entityData, offset });
                            goto RegisterEntityLoader;
                        }

                        ctor = type.GetConstructor(new Type[] { typeof(Vector2) });
                        if (ctor != null) {
                            loader = (level, levelData, offset, entityData) => (Entity) ctor.Invoke(new object[] { offset });
                            goto RegisterEntityLoader;
                        }

                        ctor = type.GetConstructor(_EmptyTypeArray);
                        if (ctor != null) {
                            loader = (level, levelData, offset, entityData) => (Entity) ctor.Invoke(_EmptyObjectArray);
                            goto RegisterEntityLoader;
                        }

                        RegisterEntityLoader:
                        if (loader == null) {
                            Logger.Log(LogLevel.Warn, "core", $"Found custom entity without suitable constructor / {genName}(Level, LevelData, Vector2, EntityData): {id} ({type.FullName})");
                            continue;
                        }
                        patch_Level.EntityLoaders[id] = loader;
                    }
                }
                // Register with the StrawberryRegistry all entities marked with RegisterStrawberryAttribute.
                foreach (RegisterStrawberryAttribute attrib in type.GetCustomAttributes<RegisterStrawberryAttribute>()) {
                    List<string> names = new List<string>();
                    foreach (CustomEntityAttribute nameAttrib in type.GetCustomAttributes<CustomEntityAttribute>())
                        foreach (string idFull in nameAttrib.IDs) {
                            string[] split = idFull.Split('=');
                            if (split.Length == 0) {
                                Logger.Log(LogLevel.Warn, "core", $"Invalid number of custom entity ID elements: {idFull} ({type.FullName})");
                                continue;
                            }
                            names.Add(split[0]);
                        }
                    if (names.Count == 0)
                        goto NoDefinedBerryNames; // no customnames? skip out on registering berry

                    foreach (string name in names) {
                        StrawberryRegistry.Register(type, name, attrib.isTracked, attrib.blocksNormalCollection);
                    }
                }
                NoDefinedBerryNames:
                ;
            }

            module.LoadSettings();
            module.Load();
            if (_Initialized)
                module.Initialize();

            InvalidateInstallationHash();

            EverestModuleMetadata meta = module.Metadata;
            meta.Hash = GetChecksum(meta);

            Logger.Log(LogLevel.Info, "core", $"Module {module.Metadata} registered.");

            // Attempt to load mods after their dependencies have been loaded.
            // Only load and lock the delayed list if we're not already loading delayed mods.
            if (Interlocked.CompareExchange(ref Loader.DelayedLock, 1, 0) == 0) {
                lock (Loader.Delayed) {
                    for (int i = Loader.Delayed.Count - 1; i > -1; i--) {
                        Tuple<EverestModuleMetadata, Action> entry = Loader.Delayed[i];
                        if (!Loader.DependenciesLoaded(entry.Item1))
                            continue;

                        Loader.LoadMod(entry.Item1);
                        Loader.Delayed.RemoveAt(i);

                        entry.Item2?.Invoke();
                    }
                }
                Interlocked.Decrement(ref Loader.DelayedLock);
            }
        }

        /// <summary>
        /// Unregisters an already registered EverestModule (mod) dynamically. Invokes Unload.
        /// </summary>
        /// <param name="module"></param>
        internal static void Unregister(this EverestModule module) {
            module.Unload();
            _DetourModManager.Unload(module.GetType().Assembly);

            // TODO: Unload from LuaLoader
            // TODO: Unload from EntityLoaders
            // TODO: Undo event listeners
            // TODO: Unload from registries
            // TODO: Make sure modules depending on this are unloaded as well.
            // TODO: Unload content, textures, audio, maps, AAAAAAAAAAAAAAAAAAAAAAA

            lock (_Modules) {
                int index = _Modules.IndexOf(module);
                _Modules.RemoveAt(index);
            }

            InvalidateInstallationHash();

            Logger.Log(LogLevel.Info, "core", $"Module {module.Metadata} unregistered.");
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
            if (AppDomain.CurrentDomain.IsDefaultAppDomain()) {
                SlowFullRestart();
                return;
            }

            Scene scene = new Scene();
            scene.HelperEntity.Add(new Coroutine(_QuickFullRestart(Engine.Scene is Overworld)));
            Engine.Scene = scene;
        }

        private static IEnumerator _QuickFullRestart(bool fromOverworld) {
            SaveData save = SaveData.Instance;
            if (save != null && save.FileSlot == patch_SaveData.LoadedModSaveDataIndex) {
                if (!fromOverworld) {
                    CoreModule.Settings.QuickRestart = save.FileSlot;
                }
                save.BeforeSave();
                UserIO.Save<SaveData>(SaveData.GetFilename(save.FileSlot), UserIO.Serialize(save));
                CoreModule.Instance.SaveSettings();
            }

            AppDomain.CurrentDomain.SetData("EverestRestart", true);
            Engine.Instance.Exit();
            yield break;
        }

        public static void SlowFullRestart() {
            Scene scene = new Scene();
            scene.HelperEntity.Add(new Coroutine(_SlowFullRestart(Engine.Scene is Overworld)));
            Engine.Scene = scene;
        }

        private static IEnumerator _SlowFullRestart(bool fromOverworld) {
            SaveData save = SaveData.Instance;
            if (save != null && save.FileSlot == patch_SaveData.LoadedModSaveDataIndex) {
                if (!fromOverworld) {
                    CoreModule.Settings.QuickRestart = save.FileSlot;
                }
                save.BeforeSave();
                UserIO.Save<SaveData>(SaveData.GetFilename(save.FileSlot), UserIO.Serialize(save));
                CoreModule.Instance.SaveSettings();
            }

            Events.Celeste.OnShutdown += BOOT.StartCelesteProcess;
            Engine.Instance.Exit();
            yield break;
        }

        public static void LogDetours() {
            List<string> detours = _DetourLog;
            _DetourLog = new List<string>();

            StringBuilder builder = new StringBuilder();
            builder.AppendLine();
            foreach (string line in detours)
                builder.AppendLine(line);

            Logger.Log(LogLevel.Info, "detours", builder.ToString());
        }

        // A shared object a day keeps the GC away!
        public readonly static Type[] _EmptyTypeArray = new Type[0];
        public readonly static object[] _EmptyObjectArray = new object[0];

    }
}
