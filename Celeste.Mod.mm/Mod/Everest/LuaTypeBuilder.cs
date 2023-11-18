using NLua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using static Celeste.Mod.Everest;
using static Celeste.Mod.Everest.LuaLoader;

namespace Celeste.Mod {
    public static class LuaTypeBuilder {

        private static readonly ConstructorInfo c_UnverifiableCodeAttribute = typeof(UnverifiableCodeAttribute).GetConstructor(new Type[] { });

        private static int Count = -1;

        private static LuaTable _SymNode;

        internal static void Initialize() {
            Stream stream = null;
            string text;
            try {
                string pathOverride = Path.Combine(PathEverest, "typebuilder.lua");
                if (File.Exists(pathOverride)) {
                    Logger.Info("Everest.LuaTypeBuilder", "Found external Lua typebuilder script.");
                    stream = new FileStream(pathOverride, FileMode.Open, FileAccess.Read);

                } else if (Content.TryGet<AssetTypeLua>("Lua/typebuilder", out ModAsset asset)) {
                    Logger.Verbose("Everest.LuaTypeBuilder", "Found built-in Lua typebuilder script.");
                    stream = asset.Stream;
                }

                if (stream == null) {
                    Logger.Warn("Everest.LuaTypeBuilder", "Lua typebuilder script not found, disabling LuaTypeBuilder.");
                    Count = -2;
                    return;
                }

                using (StreamReader reader = new StreamReader(stream))
                    text = reader.ReadToEnd();

            } finally {
                stream?.Dispose();
            }

            object[] rva = Run(text, "typebuilder.lua");

            LuaFunction invokecb = (LuaFunction) rva[0];
            InvokeCB = cb => invokecb.Call(cb)?.FirstOrDefault() as LuaTable;

            _SymNode = Symbol("node");
        }

        private static Func<LuaFunction, LuaTable> InvokeCB;

        public static Type Build(object rulesOrCB) {
            if (Count == -2)
                return null;

            Count++;

            LuaTable rules = rulesOrCB as LuaTable;
            if (rulesOrCB is LuaFunction rulesCB)
                rules = InvokeCB(rulesCB);
            if (rules == null)
                return null;

            string tns = rules["namespace"] as string;
            string tname = rules["name"] as string ?? $"LuaType{Count}";
            string tfullName = string.IsNullOrEmpty(tns) ? tname : $"{tns}.{tname}";

            AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName() {
                    Name = $"LuaDynAsm{Count}"
                },
                AssemblyBuilderAccess.Run // Collectable assemblies cannot be AssemblyResolve-d
            );

            asm.SetCustomAttribute(new CustomAttributeBuilder(c_UnverifiableCodeAttribute, new object[0]));

            ModuleBuilder module = asm.DefineDynamicModule($"{asm.GetName().Name}.dll");

            Type baset = null;
            if (baset == null && rules["__base"] is CachedType basec)
                baset = basec.Type;
            if (baset == null && rules["__base"] is string basen)
                baset = Type.GetType(basen);
            if (baset == null)
                baset = typeof(object);

            TypeBuilder builder = module.DefineType(
                tname,
                TypeAttributes.Public | TypeAttributes.Class,
                baset
            );

            foreach (KeyValuePair<object, object> kvp in rules) {
                string rname = kvp.Key as string;
                if (string.IsNullOrEmpty(rname) || rname.StartsWith("__"))
                    continue;

                if (kvp.Value is LuaFunction cb) {
                    LuaTable info = rules["__" + rname] as LuaTable;
                    if (info == null)
                        throw new InvalidDataException($"Type ruleset for {tfullName} contains function {rname} but no info!");

                    string name = info["name"] as string ?? rname;
                    Type ret = GetType(info["ret"]) ?? typeof(void);
                    Console.WriteLine(ret?.ToString() ?? "NULL");
                }
            }

            Type built = builder.CreateType();

            Assembly asmBuilt = built.Assembly;
            AppDomain.CurrentDomain.AssemblyResolve +=
                (s, e) => e.Name == asmBuilt.FullName ? asmBuilt : null;
            Precache(asmBuilt);

            return built;
        }

        private static Type GetType(object raw) {
            if (raw == null)
                return null;

            if (raw is Type type)
                return type;

            if (raw is CachedType ctype)
                return ctype.Type;

            if (raw is LuaTable table)
                return table[_SymNode] as Type;

            throw new InvalidDataException($"Expected Type, CachedType or a compatible LuaTable, got {raw.GetType()}");
        }

    }
}
