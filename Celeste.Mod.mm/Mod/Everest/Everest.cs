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

        private static byte[] _InstallationHash;
        public static byte[] InstallationHash {
            get {
                if (_InstallationHash != null)
                    return _InstallationHash;

                using (HashAlgorithm hasher = SHA256.Create()) {
                    List<byte> data = new List<byte>(512);

                    void AddFile(string path) {
                        using (FileStream fs = File.OpenRead(path))
                            AddStream(fs);
                    }
                    void AddStream(Stream stream) {
                        data.AddRange(hasher.ComputeHash(stream));
                    }

                    /* Note:
                     * I've decided to disable adding Celeste itself into the master hash
                     * as Everest updates and XNA vs FNA would just affect it too wildly.
                     * -ade
                     */
                    /*
                    string pathCeleste = typeof(Celeste).Assembly.Location;
                    if (!string.IsNullOrEmpty(pathCeleste))
                        AddFile(pathCeleste);
                    */

                    // Add all mod containers (or .DLLs).
                    lock (_Modules) {
                        foreach (EverestModule mod in _Modules) {
                            EverestModuleMetadata meta = mod.Metadata;
                            if (!string.IsNullOrEmpty(meta.PathArchive)) {
                                AddFile(meta.PathArchive);
                                continue;
                            }
                            if (!string.IsNullOrEmpty(meta.DLL)) {
                                AddFile(meta.DLL);
                                continue;
                            }
                        }
                    }

                    // Add all map .bins
                    foreach (ModAsset asset in Content.Map.Values) {
                        if (asset.Type != typeof(AssetTypeMap))
                            continue;
                        using (Stream stream = asset.Stream)
                            AddStream(stream);
                    }

                    // Return the final hash.
                    return _InstallationHash = hasher.ComputeHash(data.ToArray());
                }
            }
        }
        public static string InstallationHashShort {
            get {
                // MD5 the installation hash.
                using (HashAlgorithm hasher = MD5.Create()) {
                    return hasher.ComputeHash(InstallationHash).ToHexadecimalString();
                }
            }
        }

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

            PathGame = Path.GetDirectoryName(typeof(Celeste).Assembly.Location);
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

            if (!Flags.IsDisabled) {
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

            Loader.LoadAuto();

            if (Flags.IsHeadless) {
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
            Discord.Initialize();

            // Add the previously created managers.
            Celeste.Instance.Components.Add(TouchInputManager.Instance);

            Invoke("Initialize");
            _Initialized = true;

            // Pre-generate the hash.
            _InstallationHash = InstallationHash;
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

            _InstallationHash = null;

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
                Invoke("SaveSettings");
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
            scene.HelperEntity.Add(new Coroutine(_QuickFullRestart()));
            Engine.Scene = scene;
        }

        private static IEnumerator _QuickFullRestart() {
            SaveData save = SaveData.Instance;
            if (save != null) {
                CoreModule.Settings.QuickRestart = save.FileSlot;
                save.BeforeSave();
                UserIO.Save<SaveData>(SaveData.GetFilename(save.FileSlot), UserIO.Serialize(save));
                CoreModule.Instance.SaveSettings();
            }

            Events.Celeste.OnShutdown += () => {
                AudioExt.System?.release();
                Thread offspring = new Thread(() => {
                    Process game = new Process();
                    // If the game was installed via Steam, it should restart in a Steam context on its own.
                    if (Environment.OSVersion.Platform == PlatformID.Unix ||
                        Environment.OSVersion.Platform == PlatformID.MacOSX) {
                        // The Linux and macOS versions come with a wrapping bash script.
                        game.StartInfo.FileName = "bash";
                        game.StartInfo.Arguments = "\"" + Path.Combine(PathGame, "Celeste") + "\"";
                    } else {
                        game.StartInfo.FileName = Path.Combine(PathGame, "Celeste.exe");
                    }
                    game.StartInfo.WorkingDirectory = PathGame;
                    game.Start();
                });
                offspring.Start();
            };

            Engine.Instance.Exit();

            yield break;
        }

        // A shared object a day keeps the GC away!
        public readonly static Type[] _EmptyTypeArray = new Type[0];
        public readonly static object[] _EmptyObjectArray = new object[0];

        /// <summary>
        /// Invoke a method in all loaded EverestModules.
        /// </summary>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="args">Any arguments to be passed to the methods.</param>
        public static void Invoke(string methodName, params object[] args)
            => InvokeTyped(methodName, null, args);
        /// <summary>
        /// Invoke a method in all loaded EverestModules.
        /// </summary>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="argsTypes">The types of the arguments passed to the methods.</param>
        /// <param name="args">Any arguments to be passed to the methods.</param>
        public static void InvokeTyped(string methodName, Type[] argsTypes, params object[] args) {
            if (args == null) {
                args = _EmptyObjectArray;
                if (argsTypes == null)
                    argsTypes = _EmptyTypeArray;
            } else if (argsTypes == null) {
                argsTypes = Type.GetTypeArray(args);
            }

            if (!Debugger.IsAttached) {
                // Fast codepath: DynamicMethodDelegate
                // Unfortunately prevents us from stepping into invoked methods.
                for (int i = 0; i < _Modules.Count; i++) {
                    EverestModule module = _Modules[i];
                    IDictionary<string, FastReflectionDelegate> moduleMethods = _ModuleMethodDelegates[i];
                    FastReflectionDelegate method;

                    if (moduleMethods.TryGetValue(methodName, out method)) {
                        if (method == null)
                            continue;
                        method(module, args);
                        continue;
                    }

                    MethodInfo methodInfo = _ModuleTypes[i].GetMethod(methodName, argsTypes);
                    if (methodInfo != null)
                        method = methodInfo.GetFastDelegate();
                    moduleMethods[methodName] = method;
                    if (method == null)
                        continue;

                    method(module, args);
                }

            } else {
                // Slow codepath: MethodInfo.Invoke
                // Doesn't hinder us from stepping into the invoked methods.
                for (int i = 0; i < _Modules.Count; i++) {
                    EverestModule module = _Modules[i];
                    IDictionary<string, MethodInfo> moduleMethods = _ModuleMethods[i];
                    MethodInfo methodInfo;

                    if (moduleMethods.TryGetValue(methodName, out methodInfo)) {
                        if (methodInfo == null)
                            continue;
                        methodInfo.Invoke(module, args);
                        continue;
                    }

                    methodInfo = _ModuleTypes[i].GetMethod(methodName, argsTypes);
                    moduleMethods[methodName] = methodInfo;
                    if (methodInfo == null)
                        continue;

                    methodInfo.Invoke(module, args);
                }
            }
        }

    }
}
