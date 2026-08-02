using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Celeste.Mod.HookGen;

public sealed class HookGeneratorModder : MonoModder {
    private static readonly Dictionary<string, string> PrimitiveTypeNameMap = new() {
        { "System.Void", "void" },
        { "System.Object", "object" },
        { "System.Boolean", "bool" },
        { "System.Char", "char" },
        { "System.String", "string" },
        { "System.Single", "float" },
        { "System.Double", "double" },
        { "System.Decimal", "decimal" },
        { "System.SByte", "sbyte" },
        { "System.Byte", "byte" },
        { "System.Int16", "short" },
        { "System.Int32", "int" },
        { "System.Int64", "long" },
        { "System.UInt16", "ushort" },
        { "System.UInt32", "uint" },
        { "System.UInt64", "ulong" },
    };

    private const string OnHookNamespace = "On";
    private const string ILHookNamespace = "IL";

    private readonly ModuleDefinition outputModule;

    private readonly TypeReference t_MulticastDelegate;
    private readonly TypeReference t_AsyncCallback;
    private readonly TypeReference t_IAsyncResult;
    private readonly TypeReference t_EditorBrowsableState;

    private readonly TypeReference t_ILManipulator;

    private readonly MethodReference m_ObsoleteAttribute_ctor;
    private readonly MethodReference m_EditorBrowsableAttribute_ctor;
    private readonly MethodReference m_IgnoresAccessChecksToAttribute_ctor;

    private readonly MethodReference m_GetMethodFromHandle;
    private readonly MethodReference m_Add;
    private readonly MethodReference m_Remove;
    private readonly MethodReference m_Modify;
    private readonly MethodReference m_Unmodify;

    private static readonly string ObsoleteMessage =
        """
        Hooks on Everest-internal types/methods (=not part of the vanilla assembly) are deprecated and unsupported.
        If you have a legitimate need for creating them, please reach out so that it can be added to the official API!
        Attempting to uses On. / IL. HookGen helpers to create such hooks will fail for Core mods.
        """.Replace('\n', ' ');

    public HookGeneratorModder(ModuleDefinition inputModule, ModuleDefinition outputModule) {
        Module = inputModule;
        this.outputModule = outputModule;

        // Copy all assembly references from the input module.
        MapDependencies();
        outputModule.AssemblyReferences.AddRange(Module.AssemblyReferences);
        DependencyMap[outputModule] = new List<ModuleDefinition>(DependencyMap[Module]);

        MapDependency(Module, "MonoMod.Utils");
        if (!DependencyCache.TryGetValue("MonoMod.Utils", out var module_Utils)) {
            throw new FileNotFoundException("MonoMod.Utils not found!");
        }

        t_MulticastDelegate = outputModule.ImportReference(FindType("System.MulticastDelegate"));
        t_AsyncCallback = outputModule.ImportReference(FindType("System.AsyncCallback"));
        t_IAsyncResult = outputModule.ImportReference(FindType("System.IAsyncResult"));
        t_EditorBrowsableState = outputModule.ImportReference(FindType("System.ComponentModel.EditorBrowsableState"));

        t_ILManipulator = outputModule.ImportReference(module_Utils.GetType("MonoMod.Cil.ILContext/Manipulator"));

        // Directly target Everest's endpoint manager, instead of relinking from MonoMod's manager
        var td_LegacyHookEndpointManager = Module.GetType("Celeste.Mod.Helpers.LegacyMonoMod.LegacyHookEndpointManager");

        m_ObsoleteAttribute_ctor = outputModule.ImportReference(FindType("System.ObsoleteAttribute").Resolve()
            .FindMethod("System.Void .ctor(System.String,System.Boolean)"));
        m_EditorBrowsableAttribute_ctor = outputModule.ImportReference(FindType("System.ComponentModel.EditorBrowsableAttribute").Resolve()
            .FindMethod("System.Void .ctor(System.ComponentModel.EditorBrowsableState)"));

        m_IgnoresAccessChecksToAttribute_ctor = outputModule.ImportReference(module_Utils.GetType("System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute")
            .FindMethod("System.Void .ctor(System.String)"));

        var t_MethodBase = outputModule.ImportReference(FindType("System.Reflection.MethodBase"));
        var t_RuntimeMethodHandle = outputModule.ImportReference(FindType("System.RuntimeMethodHandle"));
        m_GetMethodFromHandle = outputModule.ImportReference(
            new MethodReference("GetMethodFromHandle", t_MethodBase, t_MethodBase) {
                Parameters = { new ParameterDefinition(t_RuntimeMethodHandle) },
            }
        );

        m_Add = outputModule.ImportReference(td_LegacyHookEndpointManager.FindMethod("Add"));
        m_Remove = outputModule.ImportReference(td_LegacyHookEndpointManager.FindMethod("Remove"));
        m_Modify = outputModule.ImportReference(td_LegacyHookEndpointManager.FindMethod("Modify"));
        m_Unmodify = outputModule.ImportReference(td_LegacyHookEndpointManager.FindMethod("Unmodify"));
    }

    public void Generate(ModuleDefinition vanillaModule) {
        foreach (var type in Module.Types) {
            GenerateType(type, vanillaModule, out var hookType, out var hookILType);
            if (hookType == null || hookILType == null || hookType.IsNested)
                continue;

            outputModule.Types.Add(hookType);
            outputModule.Types.Add(hookILType);
        }

        // Since we are accessing private members of the Celeste assembly,
        // add a hidden JIT attribute to disable access checks.
        var ignoresAccessChecksAttrib = new CustomAttribute(m_IgnoresAccessChecksToAttribute_ctor);
        ignoresAccessChecksAttrib.ConstructorArguments.Add(new CustomAttributeArgument(outputModule.TypeSystem.String, "Celeste"));
        outputModule.Assembly.CustomAttributes.Add(ignoresAccessChecksAttrib);
    }

    private void GenerateType(TypeDefinition type, ModuleDefinition vanillaModule, out TypeDefinition? onHookType, out TypeDefinition? ilHookType) {
        onHookType = ilHookType = null;

        if (type.HasGenericParameters || type.IsRuntimeSpecialName || type.Name.StartsWith("<", StringComparison.Ordinal))
            return; // TODO

        var vanillaType = vanillaModule.GetType(type.FullName);

        onHookType = new TypeDefinition(
            type.IsNested ? null : $"{OnHookNamespace}{(string.IsNullOrEmpty(type.Namespace) ? "" : $".{type.Namespace}")}",
            type.Name,
            (type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public) | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class,
            outputModule.TypeSystem.Object
        );
        ilHookType = new TypeDefinition(
            type.IsNested ? null : $"{ILHookNamespace}{(string.IsNullOrEmpty(type.Namespace) ? "" : $".{type.Namespace}")}",
            type.Name,
            (type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public) | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class,
            outputModule.TypeSystem.Object
        );

        bool add = false;

        foreach (var method in type.Methods) {
            add |= GenerateMethod(method, vanillaType, onHookType, ilHookType);
        }

        foreach (var nested in type.NestedTypes) {
            GenerateType(nested, vanillaModule, out var hookNestedType, out var hookNestedILType);
            if (hookNestedType == null || hookNestedILType == null) {
                continue;
            }

            add = true;
            onHookType.NestedTypes.Add(hookNestedType);
            ilHookType.NestedTypes.Add(hookNestedILType);
        }

        if (!add) {
            // Avoid emitting empty types
            onHookType = ilHookType = null;
        }
    }

    private bool GenerateMethod(MethodDefinition method, TypeDefinition? vanillaType, TypeDefinition onHookType, TypeDefinition ilHookType) {
        if (method.HasGenericParameters || method.IsAbstract || method is { IsSpecialName: true, IsConstructor: false })
            return false;

        if (method.Name.StartsWith("orig_", StringComparison.Ordinal))
            return false;

        // Check if this method is part of the vanilla assembly
        bool isVanilla = vanillaType != null && vanillaType.Methods
            .Any(other => {
                if (other == method) return false;
                if (other.Name != method.Name) return false;
                if (other.Parameters.Count != method.Parameters.Count) return false;

                for (int i = 0; i < method.Parameters.Count; i++) {
                    if (other.Parameters[i].ParameterType.FullName != method.Parameters[i].ParameterType.FullName) return false;
                }

                return true;
            });

        // Check if the declaring type has an 'orig_' method of the current one
        var origMethod = method.DeclaringType.Methods
            .FirstOrDefault(other => {
                if (other == method) return false;
                if (other.Name != $"orig_{method.Name}") return false;
                if (other.Parameters.Count != method.Parameters.Count) return false;

                for (int i = 0; i < method.Parameters.Count; i++) {
                    if (other.Parameters[i].ParameterType.FullName != method.Parameters[i].ParameterType.FullName) return false;
                }

                return true;
            });

        string name = GetFriendlyName(method);
        bool suffix = method.Parameters.Count != 0;

        MethodDefinition[] overloads = [];
        if (suffix) {
            overloads = method.DeclaringType.Methods.Where(other => !other.HasGenericParameters && GetFriendlyName(other) == name && other != method).ToArray();
            if (overloads.Length == 0) {
                suffix = false;
            }
        }

        if (suffix) {
            var builder = new StringBuilder();
            for (int paramIdx = 0; paramIdx < method.Parameters.Count; paramIdx++) {
                var param = method.Parameters[paramIdx];
                if (!PrimitiveTypeNameMap.TryGetValue(param.ParameterType.FullName, out string? typeName)) {
                    typeName = GetFriendlyName(param.ParameterType, full: false);
                }

                if (overloads.Any(other => {
                        var otherParam = other.Parameters.ElementAtOrDefault(paramIdx);
                        return
                            otherParam != null &&
                            GetFriendlyName(otherParam.ParameterType, false) == typeName &&
                            otherParam.ParameterType.Namespace != param.ParameterType.Namespace;
                    })) {
                    typeName = GetFriendlyName(param.ParameterType, true);
                }

                builder.Append('_');
                builder.Append(typeName.Replace(".", "", StringComparison.Ordinal).Replace("`", "", StringComparison.Ordinal));
            }

            name += builder.ToString();
        }

        var origDelegate = GenerateDelegate(method, []);
        origDelegate.Name = $"orig_{name}";
        origDelegate.CustomAttributes.Add(GenerateEditorBrowsable(EditorBrowsableState.Never));
        onHookType.NestedTypes.Add(origDelegate);

        var hookDelegate = GenerateDelegate(method, [new ParameterDefinition("orig", ParameterAttributes.None, origDelegate)]);
        hookDelegate.Name = "hook_" + name;
        hookDelegate.CustomAttributes.Add(GenerateEditorBrowsable(EditorBrowsableState.Never));
        onHookType.NestedTypes.Add(hookDelegate);

        ILCursor cur;
        GenericInstanceMethod endpointMethod;

        #region On-Hook

        var methodRef = outputModule.ImportReference(method);

        var addOnHook = new MethodDefinition(
            "add_" + name,
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static,
            outputModule.TypeSystem.Void
        );
        addOnHook.Parameters.Add(new ParameterDefinition(null, ParameterAttributes.None, hookDelegate));
        addOnHook.Body = new MethodBody(addOnHook);
        onHookType.Methods.Add(addOnHook);

        cur = new ILCursor(new ILContext(addOnHook));
        cur.EmitLdtoken(methodRef);
        cur.EmitCall(m_GetMethodFromHandle);
        cur.EmitLdarg0();
        endpointMethod = new GenericInstanceMethod(m_Add);
        endpointMethod.GenericArguments.Add(hookDelegate);
        cur.EmitCall(endpointMethod);
        cur.EmitRet();

        var removeOnHook = new MethodDefinition(
            "remove_" + name,
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static,
            outputModule.TypeSystem.Void
        );
        removeOnHook.Parameters.Add(new ParameterDefinition(null, ParameterAttributes.None, hookDelegate));
        removeOnHook.Body = new MethodBody(removeOnHook);
        onHookType.Methods.Add(removeOnHook);

        cur = new ILCursor(new ILContext(removeOnHook));
        cur.EmitLdtoken(methodRef);
        cur.EmitCall(m_GetMethodFromHandle);
        cur.EmitLdarg0();
        endpointMethod = new GenericInstanceMethod(m_Remove);
        endpointMethod.GenericArguments.Add(hookDelegate);
        cur.EmitCall(endpointMethod);
        cur.EmitRet();

        var onHookEvent = new EventDefinition(name, EventAttributes.None, hookDelegate) {
            AddMethod = addOnHook,
            RemoveMethod = removeOnHook
        };

        onHookType.Events.Add(onHookEvent);

        #endregion

        #region IL-Hook

        // If available, the IL-hook will target the 'orig_' method,
        // since it's the one containing the vanilla IL instructions.
        var origMethodRef = origMethod == null ? null : outputModule.ImportReference(origMethod);
        if (origMethod != null) {
            Log($"Orig: {method} // {origMethod}");
        }

        var addIlHook = new MethodDefinition(
            "add_" + name,
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static,
            outputModule.TypeSystem.Void
        );
        addIlHook.Parameters.Add(new ParameterDefinition(null, ParameterAttributes.None, t_ILManipulator));
        addIlHook.Body = new MethodBody(addIlHook);
        ilHookType.Methods.Add(addIlHook);

        cur = new ILCursor(new ILContext(addIlHook));
        cur.EmitLdtoken(origMethodRef ?? methodRef);
        cur.EmitCall(m_GetMethodFromHandle);
        cur.EmitLdarg0();
        endpointMethod = new GenericInstanceMethod(m_Modify);
        endpointMethod.GenericArguments.Add(hookDelegate);
        cur.EmitCall(endpointMethod);
        cur.EmitRet();

        var removeIlHook = new MethodDefinition(
            "remove_" + name,
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static,
            outputModule.TypeSystem.Void
        );
        removeIlHook.Parameters.Add(new ParameterDefinition(null, ParameterAttributes.None, t_ILManipulator));
        removeIlHook.Body = new MethodBody(removeIlHook);
        ilHookType.Methods.Add(removeIlHook);

        cur = new ILCursor(new ILContext(removeIlHook));
        cur.EmitLdtoken(origMethodRef ?? methodRef);
        cur.EmitCall(m_GetMethodFromHandle);
        cur.EmitLdarg0();
        endpointMethod = new GenericInstanceMethod(m_Unmodify);
        endpointMethod.GenericArguments.Add(hookDelegate);
        cur.EmitCall(endpointMethod);
        cur.EmitRet();

        var ilHookEvent = new EventDefinition(name, EventAttributes.None, t_ILManipulator) {
            AddMethod = addIlHook,
            RemoveMethod = removeIlHook
        };
        ilHookType.Events.Add(ilHookEvent);

        #endregion

        // Hooking Everest internals is no longer supported.
        // The events are kept for backwards compatibility.
        if (!isVanilla) {
            onHookEvent.CustomAttributes.Add(GenerateObsolete(ObsoleteMessage, error: true));
            ilHookEvent.CustomAttributes.Add(GenerateObsolete(ObsoleteMessage, error: true));
        }

        return true;
    }

    private TypeDefinition GenerateDelegate(MethodDefinition method, ParameterDefinition[] prefixParameters) {
        var delegateType = new TypeDefinition(
            null, null,
            TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.Class,
            t_MulticastDelegate
        );
        var ctor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.ReuseSlot,
            outputModule.TypeSystem.Void
        ) {
            ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed,
            HasThis = true
        };
        ctor.Parameters.Add(new ParameterDefinition(outputModule.TypeSystem.Object));
        ctor.Parameters.Add(new ParameterDefinition(outputModule.TypeSystem.IntPtr));
        ctor.Body = new MethodBody(ctor);
        delegateType.Methods.Add(ctor);

        var invoke = new MethodDefinition(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            outputModule.ImportReference(method.ReturnType)
        ) {
            ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed,
            HasThis = true
        };

        foreach (var paramDef in prefixParameters) {
            invoke.Parameters.Add(paramDef);
        }

        if (!method.IsStatic) {
            TypeReference selfType = outputModule.ImportReference(method.DeclaringType);
            if (method.DeclaringType.IsValueType) {
                selfType = new ByReferenceType(selfType);
            }

            invoke.Parameters.Add(new ParameterDefinition("self", ParameterAttributes.None, selfType));
        }

        foreach (var param in method.Parameters) {
            invoke.Parameters.Add(new ParameterDefinition(
                param.Name,
                param.Attributes & ~ParameterAttributes.Optional & ~ParameterAttributes.HasDefault,
                outputModule.ImportReference(param.ParameterType)
            ));
        }

        invoke.Body = new MethodBody(invoke);
        delegateType.Methods.Add(invoke);

        var beginInvoke = new MethodDefinition(
            "BeginInvoke",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            t_IAsyncResult
        ) {
            ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed,
            HasThis = true
        };

        foreach (var param in invoke.Parameters) {
            beginInvoke.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
        }

        beginInvoke.Parameters.Add(new ParameterDefinition("callback", ParameterAttributes.None, t_AsyncCallback));
        beginInvoke.Parameters.Add(new ParameterDefinition(null, ParameterAttributes.None, outputModule.TypeSystem.Object));
        beginInvoke.Body = new MethodBody(beginInvoke);
        delegateType.Methods.Add(beginInvoke);

        var endInvoke = new MethodDefinition(
            "EndInvoke",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            outputModule.TypeSystem.Object
        ) {
            ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed,
            HasThis = true
        };

        endInvoke.Parameters.Add(new ParameterDefinition("result", ParameterAttributes.None, t_IAsyncResult));
        endInvoke.Body = new MethodBody(endInvoke);
        delegateType.Methods.Add(endInvoke);

        return delegateType;
    }

    private CustomAttribute GenerateObsolete(string message, bool error) {
        var attrib = new CustomAttribute(m_ObsoleteAttribute_ctor);
        attrib.ConstructorArguments.Add(new CustomAttributeArgument(outputModule.TypeSystem.String, message));
        attrib.ConstructorArguments.Add(new CustomAttributeArgument(outputModule.TypeSystem.Boolean, error));
        return attrib;
    }

    private CustomAttribute GenerateEditorBrowsable(EditorBrowsableState state) {
        var attrib = new CustomAttribute(m_EditorBrowsableAttribute_ctor);
        attrib.ConstructorArguments.Add(new CustomAttributeArgument(t_EditorBrowsableState, state));
        return attrib;
    }

    // Generate a name which is usable inside C#
    private static string GetFriendlyName(MethodReference method) {
        string name = method.Name;
        if (name.StartsWith('.')) {
            name = name[1..];
        }

        name = name.Replace('.', '_');
        return name;
    }

    private static string GetFriendlyName(TypeReference type, bool full) {
        var builder = new StringBuilder();
        BuildFriendlyName(builder, type, full);
        return builder.ToString();
    }

    private static void BuildFriendlyName(StringBuilder builder, TypeReference type, bool full) {
        if (type is not TypeSpecification typeSpec) {
            builder.Append((full ? type.FullName : type.Name).Replace("_", "", StringComparison.Ordinal));
            return;
        }

        if (typeSpec.IsByReference) {
            builder.Append("ref");
        } else if (typeSpec.IsPointer) {
            builder.Append("ptr");
        }

        BuildFriendlyName(builder, typeSpec.ElementType, full);

        if (typeSpec.IsArray) {
            builder.Append("Array");
        }
    }
}