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
        private static List<Type> _ModuleTypes = new List<Type>();
        private static List<IDictionary<string, MethodInfo>> _ModuleMethods = new List<IDictionary<string, MethodInfo>>();
        private static List<IDictionary<string, FastReflectionDelegate>> _ModuleMethodDelegates = new List<IDictionary<string, FastReflectionDelegate>>();

        /// <summary>
        /// The path to the directory holding Celeste.exe
        /// </summary>
        public static string PathGame { get; internal set; }

        /// <summary>
        /// The path to the Everest /ModSettings directory.
        /// </summary>
        public static string PathSettings { get; internal set; }
        /// <summary>
        /// Whether XDG paths should be used.
        /// </summary>
        public static bool XDGPaths { get; internal set; }
        /// <summary>
        /// Path to Everest base location. Defaults to the game directory.
        /// </summary>
        public static string PathEverest { get; internal set; }

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
                        data.AddRange(mod.Metadata.Hash);
                        EverestModuleMetadata meta = mod.Metadata;
                    }
                }

                // Add all map .bins
                lock (Content.Map) {
                    foreach (ModAsset asset in Content.Map.Values.ToArray()) {
                        if (asset.Type != typeof(AssetTypeMap))
                            continue;
                        using (Stream stream = asset.Stream)
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

                else if (arg == "--everest-disabled" || arg == "--speedrun")
                    Environment.SetEnvironmentVariable("EVEREST_DISABLED", "1");

                else if (arg == "--whitelist" && queue.Count >= 1)
                    Loader.NameWhitelist = queue.Dequeue();

            }
        }

        internal static void Boot() {
            Logger.Log(LogLevel.Info, "core", "Booting Everest");
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
                if (!asmName.Name.StartsWith("Mono.Cecil") && !asmName.Name.StartsWith("YamlDotNet"))
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
                var dataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                Directory.CreateDirectory(PathEverest = Path.Combine(dataDir, "Everest"));
                Directory.CreateDirectory(Path.Combine(dataDir, "Everest", "Mods")); // Make sure it exists before content gets initialized
            }
            PathSettings = Path.Combine(PathEverest, "ModSettings");
            Directory.CreateDirectory(PathSettings);

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
        }

        /// <summary>
        /// Register a new EverestModule (mod) dynamically. Invokes LoadSettings and Load.
        /// </summary>
        /// <param name="module">Mod to register.</param>
        public static void Register(this EverestModule module) {
            lock (_Modules) {
                _Modules.Add(module);
                _ModuleTypes.Add(module.GetType());
                _ModuleMethods.Add(new Dictionary<string, MethodInfo>());
                _ModuleMethodDelegates.Add(new Dictionary<string, FastReflectionDelegate>());
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
        public static void Unregister(this EverestModule module) {
            module.Unload();

            lock (_Modules) {
                int index = _Modules.IndexOf(module);
                _Modules.RemoveAt(index);
                _ModuleTypes.RemoveAt(index);
                _ModuleMethods.RemoveAt(index);
            }

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
            Scene scene = new Scene();
            scene.HelperEntity.Add(new Coroutine(_QuickFullRestart(Engine.Scene is Overworld)));
            Engine.Scene = scene;
        }

        private static IEnumerator _QuickFullRestart(bool fromOverworld) {
            SaveData save = SaveData.Instance;
            if (save != null) {
                if (!fromOverworld) {
                    CoreModule.Settings.QuickRestart = save.FileSlot;
                }
                save.BeforeSave();
                UserIO.Save<SaveData>(SaveData.GetFilename(save.FileSlot), UserIO.Serialize(save));
                CoreModule.Instance.SaveSettings();
            }

            Events.Celeste.OnShutdown += () => {
                Thread offspring = new Thread(() => {
                    Process game = new Process();
                    // If the game was installed via Steam, it should restart in a Steam context on its own.
                    if (Environment.OSVersion.Platform == PlatformID.Unix ||
                        Environment.OSVersion.Platform == PlatformID.MacOSX) {
                        // The Linux and macOS versions come with a wrapping bash script.
                        game.StartInfo.FileName = Path.Combine(PathGame, "Celeste");
                    } else {
                        game.StartInfo.FileName = Path.Combine(PathGame, "Celeste.exe");
                    }
                    game.StartInfo.WorkingDirectory = PathGame;
                    game.Start();
                });
                offspring.Start();
                patch_Audio.System?.release();
            };

            Engine.Instance.Exit();

            yield break;
        }

        // A shared object a day keeps the GC away!
        public readonly static Type[] _EmptyTypeArray = new Type[0];
        public readonly static object[] _EmptyObjectArray = new object[0];

    }
}
