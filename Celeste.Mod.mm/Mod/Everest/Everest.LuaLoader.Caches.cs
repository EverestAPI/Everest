using KeraLua;
using NLua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

                public int Count => NamespaceMap.Count + TypeMap.Count;

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
                public readonly CachedNamespace Namespace;
                public readonly CachedType Parent;
                public readonly string FullName;
                public readonly Type Type;


                public readonly Dictionary<string, CachedType> NestedTypeMap = new Dictionary<string, CachedType>();
                public CachedType[] NestedTypes => NestedTypeMap.Values.ToArray();

                private LuaTable nilMemberTable;
                private Dictionary<string, LuaTable> membersCache;

                private CachedType(Type type) {
                    Name = type.Name;
                    Type = type;
                    FullName = type.FullName;

                    _Crawl();
                }

                public CachedType(CachedNamespace ns, Type type)
                    : this(type) {
                    Namespace = ns;
                }

                public CachedType(CachedType parent, Type type)
                    : this(type) {
                    Parent = parent;
                }

                private void _Crawl() {
                    foreach (Type type in Type.GetNestedTypes()) {
                        if (!type.IsNestedPublic)
                            continue;

                        string part = type.Name;
                        CachedType ctype = new CachedType(this, type);
                        NestedTypeMap[part] = ctype;
                        AllTypes[ctype.FullName] = ctype;
                    }
                }

                public LuaTable GetMembers(string key) {
                    if (membersCache == null) {
                        // Populate cache with PUBLIC members (the old code used all members but only public ones are actually accesible through NLua)
                        membersCache = new Dictionary<string, LuaTable>();
                        foreach (MemberInfo info in Type.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)) {
                            Type mtype = (info as PropertyInfo)?.PropertyType ?? (info as FieldInfo)?.FieldType;

                            // Cache regular name
                            string name = info.Name;
                            if (!membersCache.TryGetValue(name, out LuaTable entry)) {
                                entry = NewTable();
                                entry[1] = NewTable();
                                entry[2] = mtype;
                                membersCache.Add(name, entry);
                            }
                            InsertIntoTable((LuaTable) entry[1], info);

                            // Cache Lua-ified name
                            string luaName = string.Empty;
                            for (int i = 0; i < name.Length; i++) {
                                if (char.IsLower(name[i]))
                                    break;
                                luaName += char.ToLower(name[i]);
                            }
                            luaName += name.Substring(luaName.Length);

                            if (name != luaName) {
                                if (!membersCache.TryGetValue(luaName, out LuaTable luaEntry)) {
                                    luaEntry = NewTable();
                                    luaEntry[1] = NewTable();
                                    luaEntry[2] = mtype;
                                    membersCache.Add(luaName, luaEntry);
                                }
                                InsertIntoTable((LuaTable) luaEntry[1], info);
                            }
                        }
                    }

                    // Lookup in cache
                    if (membersCache.TryGetValue(key, out LuaTable memberTable))
                        return memberTable;

                    if (nilMemberTable == null) {
                        nilMemberTable = NewTable();
                        nilMemberTable[1] = nilMemberTable[2] = null;
                    }

                    return nilMemberTable;
                }

                private static LuaTable NewTable() {
                    Context.State.NewTable();
                    return new LuaTable(Context.State.Ref(LuaRegistry.Index), Context);
                }

                // This could be otpimized but eh
                private static void InsertIntoTable(LuaTable table, object val) => table[table.Keys.Count + 1] = val;
            }

        }
    }
}
