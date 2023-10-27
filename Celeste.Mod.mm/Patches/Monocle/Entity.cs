using System;
using MonoMod;
using Celeste.Mod.Entities;
using Celeste;
using System.Xml;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using Mono.Cecil;
using MonoMod.Utils;
using MonoMod.InlineRT;
using Mono.Cecil.Cil;
using System.Text.RegularExpressions;
using Monocle;

namespace Monocle {
    class patch_Entity : Entity {

        [MonoModIgnore]
        [MonoModConstructor]
        [PatchEntityCtor]
        public extern void ctor(Vector2 position);

        public new Scene Scene {
            [MonoModIgnore]
            get;
            [MonoModIgnore]
            private set;
        }

        public EntityData EntityData;
        public event Action<Entity> PreUpdate;
        public event Action<Entity> PostUpdate;

        internal void DissociateFromScene() {
            Scene = null;
        }

        internal void _PreUpdate() => PreUpdate?.Invoke(this);

        internal void _PostUpdate() => PostUpdate?.Invoke(this);

        public EntityID __EntityID => new EntityID(EntityData.Level.Name, EntityData.ID);
    }
}

namespace MonoMod {
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchEntityCtor))]
    class PatchEntityCtorAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchEntityCtor(ILContext context, CustomAttribute attrib) {
            FieldReference f_EntityData = context.Method.DeclaringType.FindField("EntityData");
            MethodReference f_Level_LinkEntityToData = MonoModRule.Modder.Module.GetType("Celeste.Level").FindMethod("LinkEntityToData");

            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.Before, instr => instr.MatchLdarg(0), instr => instr.MatchLdarg(1));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldnull);
            cursor.Emit(OpCodes.Call, f_Level_LinkEntityToData);

        }
    }
}
