#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_CrystalStaticSpinner : CrystalStaticSpinner {

        private CrystalColor color;

#pragma warning disable CS0649 // this attribute is from vanilla, so it's defined in there
        private Entity filler;
#pragma warning restore CS0649

        private int ID;

        public patch_CrystalStaticSpinner(Vector2 position, bool attachToSolid, CrystalColor color)
            : base(position, attachToSolid, color) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset, CrystalColor color);

        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset, CrystalColor color) {
            orig_ctor(data, offset, color);
            ID = data.ID;
        }

        public extern void orig_Awake(Scene scene);
        public override void Awake(Scene scene) {
            if ((int) color == -1) {
                Add(new CoreModeListener(this));
                if ((scene as Level).CoreMode == Session.CoreModes.Cold) {
                    color = CrystalColor.Blue;
                } else {
                    color = CrystalColor.Red;
                }
            }

            orig_Awake(scene);
        }

        [MonoModReplace]
        private void OnShake(Vector2 amount) {
            foreach (Component component in Components) {
                if (component is Image image) {
                    // change from vanilla: instead of setting the position, add to it.
                    image.Position += amount;
                }
            }

            // addition from vanilla: also shake spinner connectors.
            if (filler != null) {
                foreach (Component component in filler.Components) {
                    if (component is Image image) {
                        image.Position += amount;
                    }
                }
            }
        }

        [MonoModIgnore] // do not change anything in the method...
        [PatchSpinnerCreateSprites] // ... except manually manipulating it via MonoModRules
        private extern void CreateSprites();

        [MonoModIgnore]
        private class CoreModeListener : Component {
            public CoreModeListener(CrystalStaticSpinner parent)
                : base(true, false) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the CrystalStaticSpinner.AddSprites method to make it more efficient.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchSpinnerCreateSprites))]
    class PatchSpinnerCreateSpritesAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchSpinnerCreateSprites(ILContext context, CustomAttribute attrib) {
            FieldDefinition f_ID = context.Method.DeclaringType.FindField("ID");

            ILCursor cursor = new ILCursor(context);

            // instead of comparing the X positions for spinners, compare their IDs.
            // this way, we are sure spinner 1 will connect to spinner 2, but spinner 2 won't connect to spinner 1.
            cursor.GotoNext(instr => instr.OpCode == OpCodes.Ldloc_S,
                instr => instr.OpCode == OpCodes.Ldarg_0,
                instr => instr.OpCode == OpCodes.Beq_S);
            // Move after `ldloc_s`
            cursor.Goto(cursor.Next, MoveType.After);
            cursor.Emit(OpCodes.Ldfld, f_ID);
            // Move after `ldarg_0`
            cursor.Goto(cursor.Next, MoveType.After);
            cursor.Emit(OpCodes.Ldfld, f_ID);
            // Replace `beq_s`(!=) with `ble_s`(>)
            cursor.Next.OpCode = OpCodes.Ble_S;

            // the other.X >= this.X check is made redundant by the patch above. Remove it.
            cursor.GotoNext(instr => instr.OpCode == OpCodes.Ldloc_S,
                instr => instr.MatchCallvirt("Monocle.Entity", "get_X"));
            cursor.RemoveRange(5);

            // replace `(item.Position - Position).Length() < 24f` with `.LengthSquared() < 576f`.
            // this is equivalent, except it skips a square root calculation, which helps with performance.
            cursor.GotoNext(MoveType.After, instr => instr.MatchCall("Microsoft.Xna.Framework.Vector2", "Length"));
            cursor.Prev.Operand = context.Module.ImportReference(((MethodReference) cursor.Prev.Operand).DeclaringType.Resolve().FindMethod("System.Single LengthSquared()"));
            cursor.Next.Operand = 576f;
        }

    }
}
