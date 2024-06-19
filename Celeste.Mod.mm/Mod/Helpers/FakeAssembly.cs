using MonoMod;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace Celeste.Mod.Helpers {
    public sealed class FakeAssembly : Assembly {

        // If you thought FakeFileStream was overkill, watch this. -ade
        // Seriously though, why is Assembly abstract?!

        // This class gets used as the "executing" assembly, forcing Celeste to find types in mods.

        public readonly Assembly Inner;

        public FakeAssembly(Assembly inner) {
            Inner = inner;
        }

        private static FakeAssembly _EntryAssembly;
        [MonoModLinkFrom("System.Reflection.Assembly System.Reflection.Assembly::GetEntryAssembly()")]
        public static Assembly GetFakeEntryAssembly()
            => _EntryAssembly ?? (_EntryAssembly = new FakeAssembly(typeof(Celeste).Assembly));

        [MonoModLinkTo("System.Reflection.Assembly", "System.Reflection.Assembly GetEntryAssembly()")]
        [MonoModRemove]
        public extern static Assembly GetActualEntryAssembly();

        public override Type[] GetTypes() {
            HashSet<Assembly> added = new HashSet<Assembly>();
            List<Type> types = new List<Type>();
            // Everest.Modules contains CoreModule, which is inside the executing assembly.
            foreach (EverestModule module in Everest._Modules) {
                Assembly asm = module.GetType().Assembly;
                if (added.Contains(asm))
                    continue;
                added.Add(asm);
                types.AddRange(asm.GetTypesSafe());
            }
            return types.ToArray();
        }

        public override Type[] GetExportedTypes() {
            HashSet<Assembly> added = new HashSet<Assembly>();
            List<Type> types = new List<Type>();
            // Everest.Modules contains CoreModule, which is inside the executing assembly.
            foreach (EverestModule module in Everest._Modules) {
                Assembly asm = module.GetType().Assembly;
                if (added.Contains(asm))
                    continue;
                added.Add(asm);
                types.AddRange(asm.GetExportedTypes());
            }
            return types.ToArray();
        }

        public override Type GetType(string name) {
            // Everest.Modules contains CoreModule, which is inside the executing assembly.
            foreach (EverestModule module in Everest._Modules) {
                Assembly asm = module.GetType().Assembly;
                Type type = asm.GetType(name, false);
                if (type != null)
                    return type;
            }
            return Inner.GetType(name);
        }

        public override Type GetType(string name, bool throwOnError) {
            // Everest.Modules contains CoreModule, which is inside the executing assembly.
            foreach (EverestModule module in Everest._Modules) {
                Assembly asm = module.GetType().Assembly;
                Type type = asm.GetType(name, false);
                if (type != null)
                    return type;
            }
            return Inner.GetType(name, throwOnError);
        }

        public override Type GetType(string name, bool throwOnError, bool ignoreCase) {
            // Everest.Modules contains CoreModule, which is inside the executing assembly.
            foreach (EverestModule module in Everest._Modules) {
                Assembly asm = module.GetType().Assembly;
                Type type = asm.GetType(name, false, ignoreCase);
                if (type != null)
                    return type;
            }
            return Inner.GetType(name, throwOnError, ignoreCase);
        }

        public override bool IsDefined(Type attributeType, bool inherit) {
            // Everest.Modules contains CoreModule, which is inside the executing assembly.
            foreach (EverestModule module in Everest._Modules) {
                Assembly asm = module.GetType().Assembly;
                if (asm.IsDefined(attributeType, inherit))
                    return true;
            }
            return Inner.IsDefined(attributeType, inherit);
        }

        // All following overrides just invoke Inner.* where possible.

        public override MethodInfo EntryPoint {
            get {
                return Inner.EntryPoint;
            }
        }

        public override string FullName {
            get {
                return Inner.FullName;
            }
        }

        public override string Location {
            get {
                return Inner.Location;
            }
        }

        public override event ModuleResolveEventHandler ModuleResolve {
            add {
                Inner.ModuleResolve += value;
            }
            remove {
                Inner.ModuleResolve -= value;
            }
        }

        /*
        public override object CreateInstance(string typeName) {
            return Inner.CreateInstance(typeName);
        }

        public override object CreateInstance(string typeName, bool ignoreCase) {
            return Inner.CreateInstance(typeName, ignoreCase);
        }
        */

        public override object CreateInstance(string typeName, bool ignoreCase, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture, object[] activationAttributes) {
            return Inner.CreateInstance(typeName, ignoreCase, bindingAttr, binder, args, culture, activationAttributes);
        }

        public override object[] GetCustomAttributes(bool inherit) {
            return Inner.GetCustomAttributes(inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) {
            return Inner.GetCustomAttributes(attributeType, inherit);
        }

        public override FileStream GetFile(string name) {
            return Inner.GetFile(name);
        }

        public override FileStream[] GetFiles() {
            return Inner.GetFiles();
        }

        public override FileStream[] GetFiles(bool getResourceModules) {
            return Inner.GetFiles(getResourceModules);
        }

        /*
        public override Module[] GetLoadedModules() {
            return Inner.GetLoadedModules();
        }
        */

        public override Module[] GetLoadedModules(bool getResourceModules) {
            return Inner.GetLoadedModules(getResourceModules);
        }

        public override ManifestResourceInfo GetManifestResourceInfo(string resourceName) {
            return Inner.GetManifestResourceInfo(resourceName);
        }

        public override string[] GetManifestResourceNames() {
            return Inner.GetManifestResourceNames();
        }

        public override Stream GetManifestResourceStream(string name) {
            return Inner.GetManifestResourceStream(name);
        }

        public override Stream GetManifestResourceStream(Type type, string name) {
            return Inner.GetManifestResourceStream(type, name);
        }

        public override Module GetModule(string name) {
            return Inner.GetModule(name);
        }

        /*
        public override Module[] GetModules() {
            return Inner.GetModules();
        }
        */

        public override Module[] GetModules(bool getResourceModules) {
            return Inner.GetModules(getResourceModules);
        }

        public override AssemblyName GetName() {
            return Inner.GetName();
        }

        public override AssemblyName GetName(bool copiedName) {
            return Inner.GetName(copiedName);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            Inner.GetObjectData(info, context);
        }

        public override AssemblyName[] GetReferencedAssemblies() {
            return Inner.GetReferencedAssemblies();
        }

        public override Assembly GetSatelliteAssembly(CultureInfo culture) {
            return Inner.GetSatelliteAssembly(culture);
        }

        public override Assembly GetSatelliteAssembly(CultureInfo culture, Version version) {
            return Inner.GetSatelliteAssembly(culture, version);
        }

        /*
        public override Module LoadModule(string moduleName, byte[] rawModule) {
            return Inner.LoadModule(moduleName, rawModule);
        }
        */

        public override Module LoadModule(string moduleName, byte[] rawModule, byte[] rawSymbolStore) {
            return Inner.LoadModule(moduleName, rawModule, rawSymbolStore);
        }

    }
}
