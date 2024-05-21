using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Linq;
using System.Reflection;
using ICustomAttributeProvider = Mono.Cecil.ICustomAttributeProvider;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace MonoMod {
#region Helper Patch Attributes
    /// <summary>
    /// Make the marked method the new entry point.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.MakeEntryPoint))]
    class MakeEntryPointAttribute : Attribute { }

    /// <summary>
    /// Helper for patching methods force-implemented by an interface
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchInterface))]
    class PatchInterfaceAttribute : Attribute { }

    /// <summary>
    /// Forcibly changes a given member's name.
    /// </summary>
    [MonoModCustomAttribute(nameof(MonoModRules.ForceName))]
    class ForceNameAttribute : Attribute {
        public ForceNameAttribute(string name) {}
    }

    /// <summary>
    /// Patches the attributed method to replace _initblk calls with the initblk opcode.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchInitblk))]
    class PatchInitblkAttribute : Attribute { }
#endregion

    static partial class MonoModRules {

        public enum PatchTarget {
            Game, GameDependency, Mod
        }

        public static readonly AssemblyNameReference CelesteAsmRef = new AssemblyNameReference("Celeste", new Version(1, 0, 0, 0));
        public static readonly PatchTarget RulesPatchTarget;
        public static ModuleDefinition RulesModule;

        static MonoModRules() {
            // Note: It may actually be too late to set this to false.
            MonoModRule.Modder.MissingDependencyThrow = false;

            // Always write portable PDBs
            if (MonoModRule.Modder.WriterParameters.WriteSymbols)
                MonoModRule.Modder.WriterParameters.SymbolWriterProvider = new PortablePdbWriterProvider();

            // Get our own ModuleDefinition (this is the best way to do this afaik)
            string execModName = Assembly.GetExecutingAssembly().GetName().Name;
            RulesModule = MonoModRule.Modder.DependencyMap.Keys.First(mod =>
                execModName == $"{mod.Name.Substring(0, mod.Name.Length - 4)}.MonoModRules [MMILRT, ID:{MonoModRulesManager.GetId(MonoModRule.Modder)}]"
            );

            // Determine the patch targets
            if (MonoModRule.Modder.Mods.Contains(RulesModule)) {
                if (MonoModRule.Modder.FindType("Celeste.Celeste")?.SafeResolve()?.Scope == MonoModRule.Modder.Module)
                    RulesPatchTarget = PatchTarget.Game;
                else
                    RulesPatchTarget = PatchTarget.GameDependency;
            } else {
                RulesPatchTarget = PatchTarget.Mod;
            }

            // Initialize the appropriate rules
            InitCommonRules(MonoModRule.Modder);
            switch (RulesPatchTarget) {
                case PatchTarget.Game:
                    MonoModRule.Modder.Log($"[{RulesModule.Assembly.Name.Name}] Patching game executable...");
                    InitGameRules(MonoModRule.Modder);
                    break;
                case PatchTarget.GameDependency:
                    MonoModRule.Modder.Log($"[{RulesModule.Assembly.Name.Name}] Patching game dependency...");
                    InitDependencyRules(MonoModRule.Modder);
                    break;
                case PatchTarget.Mod:
                    MonoModRule.Modder.Log($"[{RulesModule.Assembly.Name.Name}] Patching mod executable...");
                    InitModRules(MonoModRule.Modder);
                    break;
            }

            // Null out the rules module reference once we are done patching
            // Note that MonoMod loads a separate copy of the rules type for each mod we patch (:catresort:)
            // So this doesn't break anything, however it prevents leaking the rules module definition
            MonoModRule.Modder.PostProcessors += _ => RulesModule = null;
        }

        private static void InitCommonRules(MonoModder modder) {
            // Check the modder version
            string monoModderAsmName = typeof(MonoModder).Assembly.GetName().Name;
            AssemblyNameReference monoModderAsmRef = RulesModule.AssemblyReferences.First(a => a.Name == monoModderAsmName);
            if (MonoModder.Version < monoModderAsmRef.Version)
                throw new Exception($"Unexpected version of MonoMod patcher: {MonoModder.Version} (expected {monoModderAsmRef.Version}+)");

            // Add common post processor
            modder.PostProcessors += CommonPostProcessor;
        }

        public static void CommonPostProcessor(MonoModder modder) {
            // Replace assembly name versions (fixes stubbed steam DLLs under Linux)
            foreach (AssemblyNameReference asmRef in modder.Module.AssemblyReferences) {
                ModuleDefinition dep = modder.DependencyMap[modder.Module].FirstOrDefault(mod => mod.Assembly.Name.Name == asmRef.Name);
                if (dep != null)
                    asmRef.Version = dep.Assembly.Name.Version;
            }

            // Post process types
            foreach (TypeDefinition type in modder.Module.Types)
                PostProcessType(modder, type);
        }

        private static void PostProcessType(MonoModder modder, TypeDefinition type) {
            // Fix enumerator decompilation
            if (type.IsCompilerGeneratedEnumerator())
                FixEnumeratorDecompile(type);

            foreach (MethodDefinition method in type.Methods) {
                // Run method processors
                OnPostProcessMethod?.Invoke(modder, method);

                // Fix short-long opcodes
                method.FixShortLongOps();
            }

            // Post-process nested types
            foreach (TypeDefinition nested in type.NestedTypes)
                PostProcessType(modder, nested);
        }

        private static event Action<MonoModder, MethodDefinition> OnPostProcessMethod;

#region Commmon Helper Methods
        public static AssemblyName GetRulesAssemblyRef(string name) => Assembly.GetExecutingAssembly().GetReferencedAssemblies().First(asm => asm.Name.Equals(name));

        public static bool ReplaceAssemblyRefs(MonoModder modder, Func<AssemblyNameReference, bool> filter, AssemblyName newRef) {
            // Check if the module has a reference affected by the filter
            if (!modder.Module.AssemblyReferences.Any(filter))
                return false;

            // Add new dependency and map it, if it not already exist
            bool hasNewRef = modder.Module.AssemblyReferences.Any(asmRef => asmRef.Name == newRef.Name);
            if (!hasNewRef) {
                AssemblyNameReference asmRef = new AssemblyNameReference(newRef.Name, newRef.Version);
                modder.Log($"[Celeste.Mod.mm] Adding assembly reference to {asmRef.FullName}");
                modder.Module.AssemblyReferences.Add(asmRef);
                modder.MapDependency(modder.Module, asmRef);
            }

            // Replace old references
            ModuleDefinition newModule = modder.DependencyMap[modder.Module].First(mod => mod.Assembly.Name.Name == newRef.Name);

            for (int i = 0; i < modder.Module.AssemblyReferences.Count; i++) {
                AssemblyNameReference asmRef = modder.Module.AssemblyReferences[i];
                if(!filter(asmRef))
                    continue;

                // Remove dependency
                modder.Log($"[Celeste.Mod.mm] Replacing assembly reference {asmRef.FullName} -> {newRef.FullName}");
                modder.Module.AssemblyReferences.RemoveAt(i--);
                modder.DependencyMap[modder.Module].RemoveAll(dep => dep.Assembly.FullName == asmRef.FullName);
                modder.RelinkModuleMap[asmRef.Name] = newModule;
            }

            return !hasNewRef;
        }

        private static bool RelinkAgainstFNA(MonoModder modder) {
            // Check if the module references either XNA or FNA
            if (!modder.Module.AssemblyReferences.Any(asmRef => asmRef.Name == "FNA" || asmRef.Name.StartsWith("Microsoft.Xna.Framework")))
                return false;

            // Replace XNA assembly references with FNA ones
            bool didReplaceXNA = ReplaceAssemblyRefs(MonoModRule.Modder, static asm => asm.Name.StartsWith("Microsoft.Xna.Framework"), GetRulesAssemblyRef("FNA"));

            // Ensure that FNA.dll can be loaded
            if (MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Game")?.SafeResolve() == null)
                throw new Exception("Failed to resolve Microsoft.Xna.Framework.Game");

            return didReplaceXNA;
        }
#endregion

#region Helper Patches
        public static void MakeEntryPoint(MethodDefinition method, CustomAttribute attrib) {
            MonoModRule.Modder.Module.EntryPoint = method;
        }

        public static void PatchInterface(MethodDefinition method, CustomAttribute attrib) {
            MethodAttributes flags = MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;
            method.Attributes |= flags;
        }

        public static void ForceName(ICustomAttributeProvider cap, CustomAttribute attrib) {
            if (cap is IMemberDefinition member)
                member.Name = (string) attrib.ConstructorArguments[0].Value;
        }

        public static void PatchInitblk(ILContext il, CustomAttribute attrib) {
            bool match = false;

            ILCursor c = new ILCursor(il);
            while (c.TryGotoNext(i => i.MatchCall(out MethodReference mref) && mref.Name == "_initblk")) {
                c.Next.OpCode = OpCodes.Initblk;
                c.Next.Operand = null;
                match = true;
            }

            if (!match) {
                throw new Exception("No call to _initblk found in " + il.Method.FullName + "!");
            }
        }

        /// <summary>
        /// Fix ILSpy unable to decompile enumerator after MonoMod patching<br />
        /// (<code>yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()</code>)
        /// </summary>
        public static void FixEnumeratorDecompile(TypeDefinition type) {
            foreach (MethodDefinition method in type.Methods) {
                new ILContext(method).Invoke(il => {
                    ILCursor cursor = new ILCursor(il);
                    while (cursor.TryGotoNext(instr => instr.MatchCallvirt(out MethodReference m) &&
                        (m.Name is "System.Collections.IEnumerable.GetEnumerator" or "System.IDisposable.Dispose" ||
                            m.Name.StartsWith("<>m__Finally")))
                    ) {
                        cursor.Next.OpCode = OpCodes.Call;
                    }
                });
            }
        }
#endregion

    }
}
