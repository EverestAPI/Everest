using MonoMod;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;

namespace NETCoreifier {
    // Only relink the non-deprecated methods
    public static class AppDomainShims {

        public enum SecurityContextSource {
            CurrentAppDomain = 0, CurrentAssembly = 1
        }

        private const string AssemblyNameFName = "System.Reflection.AssemblyName";
        private const string AssemblyBuilderFName = "System.Reflection.Emit.AssemblyBuilder";
        private const string AssemblyBuilderAccessFName = "System.Reflection.Emit.AssemblyBuilderAccess";
        private const string CustomAttributeBuilderFName = "System.Reflection.Emit.CustomAttributeBuilder";

        [MonoModLinkFrom($"{AssemblyBuilderFName} System.AppDomain::DefineDynamicAssembly({AssemblyNameFName},{AssemblyBuilderAccessFName},System.String,System.Boolean,System.Collections.Generic.IEnumerable`1<{CustomAttributeBuilderFName}>)")]
        public static AssemblyBuilder DefineDynamicAssembly(AppDomain domain, AssemblyName name, AssemblyBuilderAccess access, string dir, bool isSynchronized, IEnumerable<CustomAttributeBuilder> asmAttrs) {
            using (AssemblyLoadContext.EnterContextualReflection(Assembly.GetCallingAssembly()))
                return AssemblyBuilder.DefineDynamicAssembly(name, access, asmAttrs);
        }

        [MonoModLinkFrom($"{AssemblyBuilderFName} System.AppDomain::DefineDynamicAssembly({AssemblyNameFName},{AssemblyBuilderAccessFName},System.Collections.Generic.IEnumerable`1<{CustomAttributeBuilderFName}>,NETCoreifier.AppDomain/SecurityContextSource)")]
        public static AssemblyBuilder DefineDynamicAssembly(AppDomain domain, AssemblyName name, AssemblyBuilderAccess access, IEnumerable<CustomAttributeBuilder> asmAttrs, SecurityContextSource ctxSrc) {   
            using (AssemblyLoadContext.EnterContextualReflection(Assembly.GetCallingAssembly()))
                return AssemblyBuilder.DefineDynamicAssembly(name, access, asmAttrs);
        }

        [MonoModLinkFrom($"{AssemblyBuilderFName} System.AppDomain::DefineDynamicAssembly({AssemblyNameFName},{AssemblyBuilderAccessFName},System.String)")]
        public static AssemblyBuilder DefineDynamicAssembly(AppDomain domain, AssemblyName name, AssemblyBuilderAccess access, string dir) {
            using (AssemblyLoadContext.EnterContextualReflection(Assembly.GetCallingAssembly()))
                return AssemblyBuilder.DefineDynamicAssembly(name, access);
        }

        [MonoModLinkFrom($"{AssemblyBuilderFName} System.AppDomain::DefineDynamicAssembly({AssemblyNameFName},{AssemblyBuilderAccessFName})")]
        public static AssemblyBuilder DefineDynamicAssembly(AppDomain domain, AssemblyName name, AssemblyBuilderAccess access) {
            using (AssemblyLoadContext.EnterContextualReflection(Assembly.GetCallingAssembly()))
                return AssemblyBuilder.DefineDynamicAssembly(name, access);
        }

    }
}