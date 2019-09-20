using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NLua;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System.Reflection.Emit;
using System.Security;
using System.Collections;
using static Celeste.Mod.Everest;
using static Celeste.Mod.Everest.LuaLoader;

namespace Celeste.Mod {
    public static class LuaTypeBuilder {

        private static readonly ConstructorInfo c_UnverifiableCodeAttribute = typeof(UnverifiableCodeAttribute).GetConstructor(new Type[] { });

        private static int Count = -1;

        internal static void Initialize() {
            Stream stream = null;
            string text;
            try {
                string pathOverride = Path.Combine(PathEverest, "typebuilder.lua");
                if (File.Exists(pathOverride)) {
                    Logger.Log("Everest.LuaTypeBuilder", "Found external Lua typebuilder script.");
                    stream = new FileStream(pathOverride, FileMode.Open, FileAccess.Read);

                } else if (Content.TryGet<AssetTypeLua>("Lua/typebuilder", out ModAsset asset)) {
                    Logger.Log("Everest.LuaTypeBuilder", "Found built-in Lua typebuilder script.");
                    stream = asset.Stream;
                }

                if (stream == null) {
                    Logger.Log(LogLevel.Warn, "Everest.LuaTypeBuilder", "Lua typebuilder script not found, disabling LuaTypeBuilder.");
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
        }

        private static Func<LuaFunction, LuaTable> InvokeCB;

        public static Type Build(LuaBase rulesOrCB) {
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

            AssemblyBuilder asm = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName() {
                    Name = $"LuaDynAsm{Count}"
                },
                AssemblyBuilderAccess.RunAndCollect
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

            foreach (DictionaryEntry kvp in rules) {
                string rname = kvp.Key as string;
                if (string.IsNullOrEmpty(rname) || rname.StartsWith("__"))
                    continue;

                if (kvp.Value is LuaFunction cb) {
                    LuaTable info = rules["__" + rname] as LuaTable;

                }
            }

            return builder.CreateType();
        }

    }
}
