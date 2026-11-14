using Celeste;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;

namespace Celeste {
    class patch_CameraOffsetTrigger : CameraOffsetTrigger {

        public bool XOnly;
        public bool YOnly;

        public patch_CameraOffsetTrigger(EntityData data, Vector2 offset) : base(data, offset) { }

        [MonoModIgnore]
        [MonoModConstructor]
        [PatchCameraOffsetTriggerCtor]
        public extern void ctor(EntityData data, Vector2 offset);

        [MonoModIgnore]
        [PatchCameraOffsetTriggerOnEnter]
        public extern override void OnEnter(Player player);
    }
}

namespace MonoMod {

    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCameraOffsetTriggerCtor))]
    class PatchCameraOffsetTriggerCtorAttribute : Attribute { }

    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCameraOffsetTriggerOnEnter))]
    class PatchCameraOffsetTriggerOnEnterAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchCameraOffsetTriggerCtor(ILContext il, CustomAttribute attrib) {
            TypeDefinition declType = il.Method.DeclaringType;

            FieldDefinition f_XOnly = declType.FindField(nameof(patch_CameraOffsetTrigger.XOnly));
            FieldDefinition f_YOnly = declType.FindField(nameof(patch_CameraOffsetTrigger.YOnly));

            TypeDefinition t_EntityData = MonoModRule.Modder.FindType("Celeste.EntityData").Resolve();

            MethodDefinition m_EntityData_Bool = t_EntityData.FindMethod(nameof(EntityData.Bool));

            var cur = new ILCursor(il);

            // append:
            //  XOnly = data.Bool("xOnly", false);
            //  YOnly = data.Bool("yOnly", false);

            cur.GotoNext(MoveType.AfterLabel, instr => instr.MatchRet()); // at end, just before return

            cur.EmitLdarg0(); // this
            cur.EmitLdarg1(); // this, data
            cur.EmitLdstr("xOnly"); // this, data, "xOnly"
            cur.EmitLdcI4(0); // this, data, "xOnly", [false] 0
            cur.EmitCallvirt(m_EntityData_Bool); // this, data.Bool("xOnly", false)
            cur.EmitStfld(f_XOnly); // this.XOnly = data.Bool("xOnly", false);

            cur.EmitLdarg0(); // this
            cur.EmitLdarg1(); // this, data
            cur.EmitLdstr("yOnly"); // this, data, "yOnly"
            cur.EmitLdcI4(0); // this, data, "yOnly", [false] 0
            cur.EmitCallvirt(m_EntityData_Bool); // this, data.Bool("yOnly", false)
            cur.EmitStfld(f_YOnly); // this.YOnly = data.Bool("yOnly", false);
        }

        public static void PatchCameraOffsetTriggerOnEnter(ILContext il, CustomAttribute attrib) {
            TypeDefinition declType = il.Method.DeclaringType;

            FieldDefinition f_XOnly = declType.FindField(nameof(patch_CameraOffsetTrigger.XOnly));
            FieldDefinition f_YOnly = declType.FindField(nameof(patch_CameraOffsetTrigger.YOnly));

            FieldDefinition f_CameraOffset = declType.FindField(nameof(CameraOffsetTrigger.CameraOffset));

            TypeDefinition t_Level = MonoModRule.Modder.FindType("Celeste.Level").Resolve();

            FieldDefinition f_Level_CameraOffset = t_Level.FindField(nameof(Level.CameraOffset));

            TypeDefinition t_Vector2 = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Vector2").Resolve();

            FieldReference f_Vector2_X = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindField(nameof(Vector2.X)));
            FieldReference f_Vector2_Y = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindField(nameof(Vector2.Y)));

            TypeDefinition t_Entity = MonoModRule.Modder.FindType("Monocle.Entity").Resolve();

            GenericInstanceMethod m_SceneAs_Level = new(t_Entity.FindMethod(nameof(Entity.SceneAs)));
            m_SceneAs_Level.GenericArguments.Add(t_Level);

            var cur = new ILCursor(il);

            // change:
            //   <-- if (!XOnly) {
            //   <--     if (!YOnly)
            //  SceneAs<Level>().CameraOffset = CameraOffset; // keep vanilla subroutine just in case
            //   <--     else
            //   <--         SceneAs<Level>().CameraOffset.Y = CameraOffset.Y;
            //   <-- } else if (!YOnly)
            //   <--     SceneAs<Level>().CameraOffset.X = CameraOffset.X;

            ILLabel xOnlyLabel = cur.DefineLabel();
            ILLabel yOnlyLabel = cur.DefineLabel();

            cur.EmitLdarg0(); // this
            cur.EmitLdfld(f_XOnly); // this.XOnly
            cur.EmitBrtrue(xOnlyLabel); // if (this.XOnly) goto xOnly;

            cur.EmitLdarg0(); // this
            cur.EmitLdfld(f_YOnly); // this.YOnly
            cur.EmitBrtrue(yOnlyLabel); // if (this.YOnly) goto yOnly;

            cur.GotoNext(MoveType.AfterLabel, instr => instr.MatchRet()); // at end, just before return

            ILLabel retLabel = cur.DefineLabel();

            cur.EmitBr(retLabel); // goto ret;

            cur.MarkLabel(xOnlyLabel); // xOnly:

            cur.EmitLdarg0(); // this
            cur.EmitLdfld(f_YOnly); // this.YOnly
            cur.EmitBrtrue(retLabel); // if (this.YOnly) goto ret;

            cur.EmitLdarg0(); // this
            cur.EmitCall(m_SceneAs_Level); // this.SceneAs<Level>()
            cur.EmitLdflda(f_Level_CameraOffset); // ref this.SceneAs<Level>().CameraOffset
            cur.EmitLdarg0(); // ref this.SceneAs<Level>().CameraOffset, this
            cur.EmitLdflda(f_CameraOffset); // ref this.SceneAs<Level>().CameraOffset, ref this.CameraOffset
            cur.EmitLdfld(f_Vector2_X); // ref this.SceneAs<Level>().CameraOffset, this.CameraOffset.X
            cur.EmitStfld(f_Vector2_X); // this.SceneAs<Level>().CameraOffset.X = this.CameraOffset.X;

            cur.EmitBr(retLabel); // goto ret;

            cur.MarkLabel(yOnlyLabel); // yOnly:

            cur.EmitLdarg0(); // this
            cur.EmitCall(m_SceneAs_Level); // this.SceneAs<Level>()
            cur.EmitLdflda(f_Level_CameraOffset); // ref this.SceneAs<Level>().CameraOffset
            cur.EmitLdarg0(); // ref this.SceneAs<Level>().CameraOffset, this
            cur.EmitLdflda(f_CameraOffset); // ref this.SceneAs<Level>().CameraOffset, ref this.CameraOffset
            cur.EmitLdfld(f_Vector2_Y); // ref this.SceneAs<Level>().CameraOffset, this.CameraOffset.Y
            cur.EmitStfld(f_Vector2_Y); // this.SceneAs<Level>().CameraOffset.Y = this.CameraOffset.Y;

            cur.MarkLabel(retLabel); // ret:
        }
    }
}
