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

            public class CachedNamespace {
                public readonly string Name;
                public readonly CachedNamespace Parent;
                public readonly string FullName;

                public readonly Dictionary<string, CachedNamespace> NamespaceMap = new Dictionary<string, CachedNamespace>();
                public CachedNamespace[] Namespaces => NamespaceMap.Values.ToArray();

                public readonly Dictionary<string, CachedType> TypeMap = new Dictionary<string, CachedType>();
                public CachedType[] Types => TypeMap.Values.ToArray();

                public object[] All => TypeMap.Values.Cast<object>().Concat(NamespaceMap.Values).ToArray();

                public CachedNamespace(CachedNamespace ns, string name) {
                    Name = name;
                    Parent = ns;
                    FullName = ns?.Name == null ? name : (ns.FullName + "." + name);
                }

                public CachedNamespace GetNamespace(string key) {
                    if (NamespaceMap.TryGetValue(key, out CachedNamespace value))
                        return value;
                    return null;
                }

                public CachedType GetType(string key) {
                    if (TypeMap.TryGetValue(key, out CachedType value))
                        return value;
                    return null;
                }
            }

            public class CachedType {
                public readonly string Name;
                public readonly CachedNamespace Parent;
                public readonly string FullName;
                public readonly Type Type;

                public CachedType(CachedNamespace ns, string name, Type type) {
                    Name = name;
                    Parent = ns;
                    FullName = ns?.Name == null ? name : (ns.FullName + "." + name);
                    Type = type;
                }
            }

        }
    }
}
