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
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace Celeste.Mod {
    public static partial class Everest {
        public static partial class LuaLoader {

            public static Lua Context { get; private set; }

            private static readonly MethodInfo m_LuaFunction_Call = typeof(LuaFunction).GetMethod("Call");

            private static Func<string, bool> _Preload = name => {
                if (string.IsNullOrEmpty(name))
                    return false;

                Type type = Type.GetType(name);
                if (type != null) {
                    _LoadAssembly(type.Assembly.FullName);
                    return true;
                }

                Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly asm in asms) {
                    if (asm.GetName().Name == name) {
                        _LoadAssembly(asm.FullName);
                        return true;
                    }

                    type = asm.GetType(name);
                    if (type != null) {
                        _LoadAssembly(asm.FullName);
                        return true;
                    }
                }

                name = name + ".";
                foreach (Assembly asm in asms) {
                    foreach (Type expType in asm.GetTypes()) {
                        if (!expType.IsPublic)
                            continue;
                        if (expType.FullName.StartsWith(name)) {
                            _LoadAssembly(asm.FullName);
                            return true;
                        }
                    }
                }

                return false;
            };

            private static Func<string, string> _VFS = name => {
                if (string.IsNullOrEmpty(name))
                    return null;

                if (!Content.TryGet<AssetTypeLua>(name, out ModAsset asset))
                    return null;

                using (StreamReader reader = new StreamReader(asset.Stream))
                    return reader.ReadToEnd();
            };

            private static Func<string, string, LuaFunction, Hook> _Hook = (fromTypeName, fromID, to) => {
                Type fromType = Type.GetType(fromTypeName);
                if (fromType == null) {
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) {
                        Type type = asm.GetType(fromTypeName);
                        if (type != null) {
                            fromType = type;
                            break;
                        }
                    }
                }

                MethodBase from = null;
                IEnumerable<MethodBase> methods =
                    fromType.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Cast<MethodBase>()
                    .Concat(fromType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));

                if (!fromID.Contains(" ")) {
                    foreach (MethodInfo method in methods) {
                        if (method.GetFindableID(withType: false, simple: true) == fromID) {
                            from = method;
                            break;
                        }
                    }
                }

                if (from == null) {
                    foreach (MethodInfo method in methods) {
                        if (method.GetFindableID(withType: false) == fromID) {
                            from = method;
                            break;
                        }
                    }
                }

                ParameterInfo[] args = from.GetParameters();
                Type[] argTypes;
                Type[] argTypesOrig;

                if (!from.IsStatic) {
                    argTypesOrig = new Type[args.Length + 1];
                    argTypes = new Type[args.Length + 2];
                    argTypesOrig[0] = from.GetThisParamType();
                    argTypes[1] = from.GetThisParamType();
                    for (int i = 0; i < args.Length; i++) {
                        argTypesOrig[i + 1] = args[i].ParameterType;
                        argTypes[i + 2] = args[i].ParameterType;
                    }

                } else {
                    argTypesOrig = new Type[args.Length];
                    argTypes = new Type[args.Length + 1];
                    for (int i = 0; i < args.Length; i++) {
                        argTypesOrig[i] = args[i].ParameterType;
                        argTypes[i + 1] = args[i].ParameterType;
                    }
                }

                Type origType = HookHelper.GetDelegateType(from);
                argTypes[0] = origType;

                Type returnType = (from as MethodInfo)?.ReturnType ?? typeof(void);

                MethodInfo proxy;
                using (DynamicMethodDefinition dmd = new DynamicMethodDefinition(
                    "HookProxy_" + to.ToString(),
                    returnType,
                    argTypes
                )) {
                    ILProcessor il = dmd.GetILProcessor();

                    il.EmitReference(to);

                    il.Emit(OpCodes.Ldc_I4, argTypes.Length);
                    il.Emit(OpCodes.Newarr, typeof(object));

                    for (int i = 0; i < argTypes.Length; i++) {
                        Type argType = argTypes[i];
                        bool argIsByRef = argType.IsByRef;
                        if (argIsByRef)
                            argType = argType.GetElementType();
                        bool argIsValueType = argType.IsValueType;

                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Ldc_I4, i);
                        il.Emit(OpCodes.Ldarg, i);
                        if (argIsValueType) {
                            il.Emit(OpCodes.Box, argType);
                        }
                        il.Emit(OpCodes.Stelem_Ref);
                    }

                    il.Emit(OpCodes.Callvirt, m_LuaFunction_Call);

                    if (returnType != typeof(void)) {
                        il.Emit(OpCodes.Ldc_I4_0);
                        il.Emit(OpCodes.Ldelem_Ref);
                        if (returnType.IsValueType) {
                            il.Emit(OpCodes.Unbox_Any, returnType);
                        }
                    } else {
                        il.Emit(OpCodes.Pop);
                    }

                    il.Emit(OpCodes.Ret);

                    proxy = dmd.Generate();
                }

                // NLua hates DynamicMethods.
                string dmdType = Environment.GetEnvironmentVariable("MONOMOD_DMD_TYPE");
                Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", "MethodBuilder");
                try {
                    return new Hook(from, proxy);
                } finally {
                    Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", dmdType);
                }
            };

            private static Action<string> _LoadAssembly;

            internal static void Initialize() {
                Stream stream = null;
                string text;
                try {
                    string pathOverride = Path.Combine(PathEverest, "bootstrap.lua");
                    if (File.Exists(pathOverride)) {
                        Logger.Log("Everest.LuaLoader", "Found external Lua bootstrap script.");
                        stream = new FileStream(pathOverride, FileMode.Open, FileAccess.Read);

                    } else if (Content.TryGet<AssetTypeLua>("Lua/bootstrap", out ModAsset asset)) {
                        Logger.Log("Everest.LuaLoader", "Found built-in Lua bootstrap script.");
                        stream = asset.Stream;
                    }

                    if (stream == null) {
                        Logger.Log(LogLevel.Warn, "Everest.LuaLoader", "Lua bootstrap script not found, disabling Lua mod support.");
                        return;
                    }

                    Logger.Log("Everest.LuaLoader", "Creating Lua context and running Lua bootstrap script.");

                    using (StreamReader reader = new StreamReader(stream))
                        text = reader.ReadToEnd();

                } finally {
                    stream?.Dispose();
                }

                Context = new Lua();
                Context.UseTraceback = true;

                object[] rva = Context.DoString(text, "bootstrap");

                LuaFunction load_assembly = (LuaFunction) rva[1];
                _LoadAssembly = name => load_assembly.Call(name);
                
                LuaFunction init = (LuaFunction) rva[0];
                rva = init.Call(_Preload, _VFS, _Hook);
                if (rva != null) {
                    foreach (object rv in rva)
                        Console.WriteLine(rv);
                }
            }

        }
    }
}
