#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_HeartGem : HeartGem {

        private string fakeHeartDialog;
        private string keepGoingDialog;

        public patch_HeartGem(Vector2 position)
            : base(position) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            fakeHeartDialog = data.Attr("fakeHeartDialog", "CH9_FAKE_HEART");
            keepGoingDialog = data.Attr("keepGoingDialog", "CH9_KEEP_GOING");
        }

        [PatchHeartGemCollectRoutine]
        private extern IEnumerator orig_CollectRoutine(Player player);
        private IEnumerator CollectRoutine(Player player) {
            Level level = Scene as Level;

            bool heartIsEnd = false;
            MapMetaModeProperties mapMetaModeProperties = (level != null) ? ((patch_MapData) level.Session.MapData).Meta : null;
            if (mapMetaModeProperties != null && mapMetaModeProperties.HeartIsEnd != null) {
                heartIsEnd = mapMetaModeProperties.HeartIsEnd.Value;
            }

            heartIsEnd &= !IsFake;

            if (heartIsEnd) {
                List<IStrawberry> strawbs = new List<IStrawberry>();
                ReadOnlyCollection<Type> regBerries = StrawberryRegistry.GetBerryTypes();
                foreach (Follower follower in player.Leader.Followers) {

                    if (regBerries.Contains(follower.Entity.GetType()) && follower.Entity is IStrawberry) {
                        strawbs.Add(follower.Entity as IStrawberry);
                    }
                }
                foreach (IStrawberry strawb in strawbs) {
                    strawb.OnCollect();
                }
            }

            return orig_CollectRoutine(player);
        }

        private bool IsCompleteArea(bool value) {
            MapMetaModeProperties meta = ((patch_MapData) (Scene as Level)?.Session.MapData).Meta;
            if (meta?.HeartIsEnd != null)
                return meta.HeartIsEnd.Value && !IsFake;

            return value;
        }

        [MonoModIgnore] // don't change anything in the method...
        [PatchTotalHeartGemChecks] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        private extern void RegisterAsCollected(Level level, string poemID);

        [MonoModIgnore] // don't change anything in the method...
        [PatchFakeHeartDialog] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        private extern IEnumerator DoFakeRoutineWithBird(Player player); 

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the heart gem collection routine instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchHeartGemCollectRoutine))]
    class PatchHeartGemCollectRoutineAttribute : Attribute { }

    /// <summary>
    /// Add custom dialog to fake hearts.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFakeHeartDialog))]
    class PatchFakeHeartDialogAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchHeartGemCollectRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_IsCompleteArea = method.DeclaringType.FindMethod("System.Boolean IsCompleteArea(System.Boolean)");

            // The gem collection routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();
            FieldDefinition f_this = method.DeclaringType.FindField("<>4__this");
            FieldDefinition f_completeArea = method.DeclaringType.Fields.FirstOrDefault(f => f.Name.StartsWith("<completeArea>5__"));

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                cursor.GotoNext(instr => instr.MatchLdfld("Celeste.HeartGem", "IsFake"));
                // Push "this" onto stack, and retrieve the actual HeartGem `this`
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, f_this);

                // Pre-process the bool on stack before
                // stfld    bool Celeste.HeartGem/'<CollectRoutine>d__29'::'<completeArea>5__4'
                // No need to check for the full name when the field name itself is compiler-generated.
                // Using AfterLabel to redirect break instructions to the right place.
                cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchStfld(f_completeArea.DeclaringType.FullName, f_completeArea.Name));
                // Process.
                cursor.Emit(OpCodes.Call, m_IsCompleteArea);
            });
        }

        public static void PatchFakeHeartDialog(MethodDefinition method, CustomAttribute attrib) {
            FieldReference f_fakeHeartDialog = method.DeclaringType.FindField("fakeHeartDialog");
            FieldReference f_keepGoingDialog = method.DeclaringType.FindField("keepGoingDialog");

            // The routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();
            FieldDefinition f_this = method.DeclaringType.FindField("<>4__this");

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                cursor.GotoNext(instr => instr.MatchLdstr("CH9_FAKE_HEART"));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, f_this);
                cursor.Next.OpCode = OpCodes.Ldfld;
                cursor.Next.Operand = f_fakeHeartDialog;

                cursor.GotoNext(instr => instr.MatchLdstr("CH9_KEEP_GOING"));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, f_this);
                cursor.Next.OpCode = OpCodes.Ldfld;
                cursor.Next.Operand = f_keepGoingDialog;
            });
        }

    }
}
