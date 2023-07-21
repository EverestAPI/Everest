using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    internal static class LegacyDynamicDataCompatHooks {

        private static readonly Type DynamicData_Cache = typeof(DynamicData).GetNestedType("_Cache_", BindingFlags.NonPublic);
        private static readonly FieldInfo Cache_Setters = DynamicData_Cache.GetField("Setters");
        private static Hook cacheCtorHook;

        public static void InstallHook() {
            // Hook MonoMod itself to provide backwards compat with some MonoMod crimes
            cacheCtorHook = new Hook(
                DynamicData_Cache.GetConstructor(new Type[] { typeof(Type) }),
                typeof(LegacyDynamicDataCompatHooks).GetMethod(nameof(CacheCtorHook), BindingFlags.NonPublic | BindingFlags.Static)
            );
        }

        public static void UninstallHook() {
            cacheCtorHook?.Dispose();
            cacheCtorHook = null;
        }

        private static void CacheCtorHook(Action<object, Type> orig, object cache, Type targetType) {
            orig(cache, targetType);

            // Wrap field/property setters
            Dictionary<string, Action<object, object>> setters = (Dictionary<string, Action<object, object>>) Cache_Setters.GetValue(cache);
            for (; targetType != null && targetType != targetType.BaseType; targetType = targetType.BaseType) {
                foreach (FieldInfo field in targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    if (setters.TryGetValue(field.Name, out Action<object, object> setter))
                        setters[field.Name] = (obj, val) => setter(obj, CheckTypeMismatch(val, field.FieldType, field));

                foreach (PropertyInfo prop in targetType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    if (prop.CanWrite && setters.TryGetValue(prop.Name, out Action<object, object> setter))
                        setters[prop.Name] = (obj, val) => setter(obj, CheckTypeMismatch(val, prop.PropertyType, prop));
            }
        }

        private static object CheckTypeMismatch(object val, Type valType, MemberInfo memb) {
            if (val?.GetType()?.IsAssignableTo(valType) ?? true)
                return val;

            // A user tried to assign a field/property with an incompatible type...
            // Try to convert it, but also report this infractions
            MethodBase caller = new StackTrace().GetFrames().Select(f => f.GetMethod()).SkipWhile(m => m?.DeclaringType == typeof(LegacyDynamicDataCompatHooks)).FirstOrDefault();
            MonoModPolice.ReportMonoModCrime($"DynamicData field/property '{memb.DeclaringType.FullName}.{memb.Name}' setter type mismatch: expected {valType.FullName}, got {val?.GetType()?.FullName ?? "null"}", caller);

            return Convert.ChangeType(val, valType);
        }

    }
}