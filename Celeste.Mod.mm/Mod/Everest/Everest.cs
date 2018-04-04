using Celeste.Mod.Core;
using Monocle;
using MonoMod.Helpers;
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

namespace Celeste.Mod {
    public static partial class Everest {

        /// <summary>
        /// The currently installed Everest version in string form.
        /// </summary>
        // The following line gets replaced by Travis automatically.
        public readonly static string VersionString = "0.0.0-dev";

        /// <summary>
        /// The currently installed Everest version.
        /// </summary>
        public readonly static Version Version;
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
        /// The currently present Celeste version combined with the currently installed Everest version.
        /// </summary>
        public static string VersionCelesteString => $"{Engine.Instance.Version} [Everest: {VersionString}]";

        /// <summary>
        /// The command line arguments passed when launching the game.
        /// </summary>
        public static ReadOnlyCollection<string> Args { get; internal set; }

        /// <summary>
        /// A collection of all currently loaded EverestModules (mods).
        /// </summary>
        public static ReadOnlyCollection<EverestModule> Modules => _Modules.AsReadOnly();
        private static List<EverestModule> _Modules = new List<EverestModule>();
        private static List<Type> _ModuleTypes = new List<Type>();
        private static List<IDictionary<string, MethodInfo>> _ModuleMethods = new List<IDictionary<string, MethodInfo>>();
        private static List<IDictionary<string, DynamicMethodDelegate>> _ModuleMethodDelegates = new List<IDictionary<string, DynamicMethodDelegate>>();

        /// <summary>
        /// The path to the directory holding Celeste.exe
        /// </summary>
        public static string PathGame { get; internal set; }
        /// <summary>
        /// The path to the Everest /ModSettings directory.
        /// </summary>
        public static string PathSettings { get; internal set; }

        static Everest() {
            int versionSplitIndex = VersionString.IndexOf('-');
            if (versionSplitIndex == -1) {
                Version = new Version(VersionString);
                VersionSuffix = "";
                VersionTag = "";
                VersionCommit = "";

            } else {
                Version = new Version(VersionString.Substring(0, versionSplitIndex));
                VersionSuffix = VersionString.Substring(versionSplitIndex + 1);
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
            PathSettings = Path.Combine(PathGame, "ModSettings");
            Directory.CreateDirectory(PathSettings);

            // Before even initializing anything else, make sure to prepare any static flags.
            Flags.Initialize();

            // Initialize the content helper.
            Content.Initialize();

            // Initialize all main managers before loading any mods.
            NotificationManager.Instance = new NotificationManager(Celeste.Instance);
            TouchInputManager.Instance = new TouchInputManager(Celeste.Instance);
            // Don't add it yet, though - add it in Initialize.

            // Register our core module and load any other modules.
            new CoreModule().Register();
            Loader.LoadAuto();

            // Also let all mods parse the arguments.
            Queue<string> args = new Queue<string>(Args);
            while (args.Count > 0) {
                string arg = args.Dequeue();
                foreach (EverestModule mod in Modules) {
                    if (mod.ParseArg(arg, args))
                        break;
                }
            }

            // Start requesting the version list ASAP.
            Updater.RequestAll();
        }

        internal static void Initialize() {
            // Initialize misc stuff.
            TextInput.Initialize(Celeste.Instance);

            // Add the previously created managers.
            Celeste.Instance.Components.Add(NotificationManager.Instance);
            Celeste.Instance.Components.Add(TouchInputManager.Instance);

            Invoke("Initialize");
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
                _ModuleMethodDelegates.Add(new Dictionary<string, DynamicMethodDelegate>());
            }

            module.LoadSettings();
            module.Load();

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
            bool saving = true;
            RunThread.Start(() => {
                Invoke("SaveSettings");
                saving = false;
            }, "MOD_IO", false);

            SaveLoadIcon.Show(Engine.Scene);
            while (saving)
                yield return null;
            yield return UserIO.SaveHandler(false, true);
            SaveLoadIcon.Hide();
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
                    IDictionary<string, DynamicMethodDelegate> moduleMethods = _ModuleMethodDelegates[i];
                    DynamicMethodDelegate method;

                    if (moduleMethods.TryGetValue(methodName, out method)) {
                        if (method == null)
                            continue;
                        method(module, args);
                        continue;
                    }

                    MethodInfo methodInfo = _ModuleTypes[i].GetMethod(methodName, argsTypes);
                    if (methodInfo != null)
                        method = methodInfo.GetDelegate();
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
