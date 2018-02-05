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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static partial class Everest {

        // The following line gets replaced by Travis automatically.
        public readonly static string VersionString = "0.0.0-dev";

        public readonly static Version Version;
        public readonly static string VersionSuffix;
        static Everest() {
            int versionSplitIndex = VersionString.IndexOf('-');
            if (versionSplitIndex == -1) {
                Version = new Version(VersionString);
                VersionSuffix = null;
            } else {
                Version = new Version(VersionString.Substring(0, versionSplitIndex));
                VersionSuffix = VersionString.Substring(versionSplitIndex + 1);
            }
        }

        public static ReadOnlyCollection<string> Args { get; internal set; }

        public static ReadOnlyCollection<EverestModule> Modules => _Modules.AsReadOnly();
        private static List<EverestModule> _Modules = new List<EverestModule>();
        private static List<Type> _ModuleTypes = new List<Type>();
        private static List<IDictionary<string, MethodInfo>> _ModuleMethods = new List<IDictionary<string, MethodInfo>>();
        private static List<IDictionary<string, DynamicMethodDelegate>> _ModuleMethodDelegates = new List<IDictionary<string, DynamicMethodDelegate>>();

        public static string PathGame { get; internal set; }
        public static string PathSettings { get; internal set; }

        public static void ParseArgs(string[] args) {
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

        public static void Boot() {
            PathGame = Path.GetDirectoryName(typeof(Celeste).Assembly.Location);
            PathSettings = Path.Combine(PathGame, "ModSettings");
            Directory.CreateDirectory(PathSettings);

            // Initialize the content helper.
            Content.Initialize();

            // Register our core module and load any other modules.
            new CoreModule().Register();
            Loader.LoadAuto();

            // We're ready - invoke Load in all loaded modules, including CoreModule.
            Invoke("Load");
        }

        public static void Register(this EverestModule module) {
            module.LoadSettings();

            lock (_Modules) {
                _Modules.Add(module);
                _ModuleTypes.Add(module.GetType());
                _ModuleMethods.Add(new FastDictionary<string, MethodInfo>());
                _ModuleMethodDelegates.Add(new FastDictionary<string, DynamicMethodDelegate>());
            }

            Logger.Log("core", $"Module {module.Metadata} registered.");
        }

        public static void Unregister(this EverestModule module) {
            lock (_Modules) {
                int index = _Modules.IndexOf(module);
                _Modules.RemoveAt(index);
                _ModuleTypes.RemoveAt(index);
                _ModuleMethods.RemoveAt(index);
            }
        }

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

        public static void Invoke(string methodName, params object[] args)
            => InvokeTyped(methodName, null, args);
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
