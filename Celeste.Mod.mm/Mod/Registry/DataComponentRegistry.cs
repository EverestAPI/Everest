using Celeste.Mod.Registry;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static MonoMod.MonoModRules;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
#pragma warning disable CS0618

namespace MonoMod {
    static partial class MonoModRules {
        public static void PatchAllEntity(TypeDefinition t) {
            static bool IsEntity(TypeDefinition t) {
                while (true) {
                    if (t is null) {
                        return false;
                    }
                    if (t.Module.Assembly.Name.Name == "Celeste" && t.Name == "Entity" && t.Namespace == "Monocle" && !t.IsNested) {
                        return true;
                    }
                    t = t.BaseType?.SafeResolve();
                }
            }
            if (IsEntity(t)) {
                template ??= RulesModule.Types.First(x => x.Name == nameof(TemplateEntity) && x.Namespace == nameof(MonoMod));
                ModuleDefinition module = t.Module;
                FieldDefinition origslots = template.FindField(nameof(TemplateEntity.slots));
                var objarr = new ArrayType(module.TypeSystem.Object);
                var slots = new FieldDefinition(TemplateEntity.slotsName, FieldAttributes.Private, objarr);
                t.Fields.Add(slots);

                FieldDefinition origreg = template.FindField(nameof(TemplateEntity._registered));
                var registered = new FieldDefinition(TemplateEntity.registeredName, FieldAttributes.Private | FieldAttributes.Static, module.ImportReference(origreg.FieldType));
                t.Fields.Add(registered);

                MethodDefinition methodTemplate = template.FindMethod(nameof(TemplateEntity.ReadSlot));
                MethodDefinition method = methodTemplate.Clone();
                method.DeclaringType = null;
                t.Methods.Add(method);
                method.Name = TemplateEntity.readName;
                method.Attributes &= ~MethodAttributes.MemberAccessMask;
                method.Attributes |= MethodAttributes.Private;
                method.Parameters[1].ParameterType = t;
                foreach (Instruction item in method.Body.Instructions) {
                    if (item.Operand is FieldReference fr) {
                        if (fr.Name == origreg.Name) {
                            item.Operand = registered;
                        } else if (fr.Name == origslots.Name) {
                            item.Operand = slots;
                        }
                    }
                    // not complete but enough
                    if (item.Operand is GenericInstanceMethod mr) {
                        var nmr = new GenericInstanceMethod(mr.ElementMethod);
                        for (int i = 0; i < mr.GenericArguments.Count; i++) {
                            TypeReference p = mr.GenericArguments[i];
                            if (p is GenericParameter gp) {
                                nmr.GenericArguments.Add(method.GenericParameters[gp.Position]);
                            } else {
                                nmr.GenericArguments.Add(p);
                            }
                        }
                        item.Operand = module.ImportReference(nmr, method);
                    }
                    if (item.Operand is IMetadataTokenProvider imtp) {
                        item.Operand = module.ImportReference(imtp);
                    }
                }
            }
        }
        static TypeDefinition template;
    }
#nullable enable
    [MonoModRemove]
    internal class TemplateEntity {
        internal const string slotsName = "```" + nameof(slots);
        internal const string registeredName = "```" + nameof(_registered);
        internal const string readName = "```" + nameof(ReadSlot);
        internal object[]? slots = null!;
        internal static List<RegistryInfo?> _registered = null!;
        // note: keep it simple, especially be careful about generic method
        internal static ref TRet ReadSlot<TRet>(DataComponentRegistrySlotHolder slotHolder, TemplateEntity self) where TRet : class {
            return ref slotHolder.ReadSlot<TRet>(ref self.slots, _registered);
        }
    }
}

namespace Celeste.Mod.Registry {
    [Obsolete("don't reference it from user code.")]
    public class DataComponentRegistrySlotHolder {
        internal int slot;
        public ref TRet ReadSlot<TRet>(ref object[]? self, List<RegistryInfo?> reg) {
            if (self is null) {
                self = new object[reg.Count];
            } else if (self.Length <= slot) {
                Array.Resize(ref self, reg.Count);
            }
            return ref Unsafe.As<object, TRet>(ref self[slot]);
        }
    }

    public class RegistryInfo {
        [AllowNull] public string ModName;
        [AllowNull] public string Description;
    }
    public static class DataComponentRegistry {
        public delegate ref TRet Accessor<T, TRet>(T self) where T : Entity where TRet : class?;

        /// <summary>
        /// A performant data holder implementation.
        /// Allows you to attach any data to a type of entity.
        /// </summary>
        /// <remarks>
        /// note: register new field and then access it *may* invalidate all existing references.
        /// this can secretly happens in hook chain.
        /// be careful with this one.
        /// </remarks>
        /// <typeparam name="T">Target entity type.</typeparam>
        /// <typeparam name="TRet">Attached data type.</typeparam>
        /// <param name="info">
        /// it's mainly for external tools, and not actually used anywhere.
        /// type your modname and comment here.
        /// </param>
        /// <param name="debug">
        /// if in debug mode, enable type check.
        /// they should be not necessary, so you are allowed to disable them when publishing.
        /// </param>
        /// <returns>The field accessor. note that your field can be null if it's not initialized.</returns>
        public static Accessor<T, TRet?> RegisterFor<T, TRet>([AllowNull] RegistryInfo info, bool debug) where T : Entity where TRet : class? {
            BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            FieldInfo fieldInfo = typeof(T).GetField(TemplateEntity.registeredName, bf)!;
            var arr = (List<RegistryInfo?>?) fieldInfo.GetValue(null);
            if (arr is null) {
                arr = new();
                fieldInfo.SetValue(null, arr);
            }
            var slot = new DataComponentRegistrySlotHolder() { slot = arr.Count };
            arr.Add(info);

            Accessor<T, TRet?> reader =
                typeof(T)
                .GetMethod(TemplateEntity.readName, bf)!
                .MakeGenericMethod(typeof(TRet))
                .CreateDelegate<Accessor<T, TRet?>>(slot);

            if (debug) {
                Accessor<T, TRet?> _reader = reader;
                reader = a => {
                    ref TRet? got = ref _reader(a);
                    if (got is { } && got.GetType() != typeof(TRet)) {
                        throw new InvalidCastException();
                    }
                    return ref got;
                };
            }
            return reader;
        }
        /// <summary>
        /// A performant data holder implementation.
        /// Allows you to attach any data to a type of entity.
        /// </summary>
        /// <remarks>
        /// returns simple getter and setter.
        /// </remarks>
        /// <typeparam name="T">Target entity type.</typeparam>
        /// <typeparam name="TRet">Attached data type.</typeparam>
        /// <param name="info">
        /// it's mainly for external tools, and not actually used anywhere.
        /// type your modname and comment here.
        /// </param>
        /// <param name="debug">
        /// if in debug mode, enable type check.
        /// they should be not necessary, so you are allowed to disable them when publishing.
        /// </param>
        /// <returns>The field accessor. note that your field can be null if it's not initialized.</returns>
        public static (Action<T, TRet?> setter, Func<T, TRet?> getter) RegisterForSimple<T, TRet>([AllowNull] RegistryInfo info, bool debug) where T : Entity where TRet : class? {
            Accessor<T, TRet?> reader = RegisterFor<T, TRet>(info, debug);
            return (((e, v) => reader(e) = v), e => reader(e));
        }
    }
}
