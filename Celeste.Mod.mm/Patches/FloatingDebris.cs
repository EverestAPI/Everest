using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;

namespace Celeste {
    class patch_FloatingDebris : FloatingDebris {
        public patch_FloatingDebris(Vector2 position) : base(position) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModConstructor]
        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchFloatingDebrisCtor] // ... except for manually manipulating the method via MonoModRules
        public extern void ctor(Vector2 position);

        private void OnExplosion(Entity entity, Vector2 from) {
            OnExplode(from);
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the floating debris ctor to add an <see cref="ExplosionCollider"/> component to handle explosion interactions.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFloatingDebrisCtor))]
    class PatchFloatingDebrisCtorAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchFloatingDebrisCtor(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_ExplosionCollider = MonoModRule.Modder.Module.GetType("Celeste.Mod.Entities.ExplosionCollider");
            MethodDefinition m_ExplosionCollider_ctor = t_ExplosionCollider.FindMethod("System.Void .ctor(System.Action`2<Monocle.Entity,Microsoft.Xna.Framework.Vector2>,Monocle.Collider)");

            TypeDefinition t_FloatingDebris = context.Method.DeclaringType;
            MethodDefinition m_FloatingDebris_OnExplosion = t_FloatingDebris.FindMethod("System.Void OnExplosion(Monocle.Entity,Microsoft.Xna.Framework.Vector2)");

            TypeReference t_Action_Entity_Vector2 = m_ExplosionCollider_ctor.Parameters[0].ParameterType;
            MethodReference m_Action_Entity_Vector2_ctor = MonoModRule.Modder.Module.ImportReference(t_Action_Entity_Vector2.Resolve().FindMethod(".ctor"));
            m_Action_Entity_Vector2_ctor.DeclaringType = t_Action_Entity_Vector2; // generics are scary ..

            ILCursor cursor = new(context);

            // jump to just after the PlayerCollider is added
            cursor.GotoNext(MoveType.After,
                instr => instr.MatchNewobj("Celeste.PlayerCollider"),
                instr => instr.MatchCall("Monocle.Entity", "Add"));

            MethodReference m_Entity_Add = (MethodReference)cursor.Prev.Operand;

            // create and add an ExplosionCollider
            cursor.EmitLdarg0();

            cursor.EmitLdarg0();
            cursor.EmitLdftn(m_FloatingDebris_OnExplosion);
            cursor.EmitNewobj(m_Action_Entity_Vector2_ctor);
            cursor.EmitLdnull();
            cursor.EmitNewobj(m_ExplosionCollider_ctor);

            cursor.EmitCall(m_Entity_Add);
        }
    }
}
