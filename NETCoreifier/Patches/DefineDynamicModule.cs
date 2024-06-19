using MonoMod;
using System;
using System.Reflection.Emit;

namespace NETCoreifier {
    public static class AssemblyBuilderShims {

        private const string AssemblyBuilderFName = "System.Reflection.Emit.AssemblyBuilder";
        private const string ModuleBuilderFName = "System.Reflection.Emit.ModuleBuilder";
    
        [MonoModLinkFrom($"{ModuleBuilderFName} {AssemblyBuilderFName}::DefineDynamicModule(System.String,System.Boolean)")]
        public static ModuleBuilder DefineDynamicModule(AssemblyBuilder builder, string name, bool emitSymInfo) => builder.DefineDynamicModule(name);

        [MonoModLinkFrom($"{ModuleBuilderFName} {AssemblyBuilderFName}::DefineDynamicModule(System.String,System.String)")]
        public static ModuleBuilder DefineDynamicModule(AssemblyBuilder builder, string name, string file) => throw new NotSupportedException("Saving ModuleBuilder output to files isn no longer supported");

        [MonoModLinkFrom($"{ModuleBuilderFName} {AssemblyBuilderFName}::DefineDynamicModule(System.String,System.String,System.Boolean)")]
        public static ModuleBuilder DefineDynamicModule(AssemblyBuilder builder, string name, string file, bool emitSymInfo) => throw new NotSupportedException("Saving ModuleBuilder output to files isn no longer supported");

    }
}