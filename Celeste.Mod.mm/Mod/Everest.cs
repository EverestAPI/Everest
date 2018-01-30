using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class Everest {

        // TODO: Replace the following lines by build script automatically in the future.
        public static Version Version = new Version("0.0.0");
        public static string VersionSuffix = "dev";

        public static string VersionUI => Version + "-" + VersionSuffix;

        public static ReadOnlyCollection<EverestModule> Modules => _Modules.AsReadOnly();
        private static List<EverestModule> _Modules = new List<EverestModule>();
        private static List<Type> _ModuleTypes = new List<Type>();
        private static List<Dictionary<string, DynamicMethodDelegate>> _ModuleMethods = new List<Dictionary<string, DynamicMethodDelegate>>();

        public static void Initialize() {
            Register(new CoreEverestModule());

            // TODO: Relink and load external modules, do _everything._
            // We've got a long way ahead of us. -ade
        }

        public static void Register(EverestModule module) {
            _Modules.Add(module);
            _ModuleTypes.Add(module.GetType());
            _ModuleMethods.Add(new Dictionary<string, DynamicMethodDelegate>());
        }

        // A shared object a day keeps the GC away!
        private readonly static Type[] _EmptyTypeArray = new Type[0];
        private readonly static object[] _EmptyObjectArray = new object[0];

        public static void Invoke(string methodName, params object[] args) {
            if (args == null) {
                args = _EmptyObjectArray;
            }
            Type[] argsTypes = Type.GetTypeArray(args);

            for (int i = 0; i < _Modules.Count; i++) {
                EverestModule module = _Modules[i];
                Dictionary<string, DynamicMethodDelegate> moduleMethods = _ModuleMethods[i];
                DynamicMethodDelegate method;

                if (moduleMethods.TryGetValue(methodName, out method)) {
                    if (method == null)
                        continue;
                    method(module, args);
                    continue;
                }

                MethodInfo methodInfo = _ModuleTypes[i].GetMethod(methodName, argsTypes);
                if (methodInfo != null)
                    method = ReflectionHelper.GetDelegate(methodInfo);
                moduleMethods[methodName] = method;
                if (method == null)
                    continue;

                method(module, args);
            }
        }

    }
}
