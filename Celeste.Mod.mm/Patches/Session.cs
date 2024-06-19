#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using System;

namespace Celeste {
    public class patch_Session {
        public bool RestartedFromGolden;

        public patch_Session(AreaKey area, string checkpoint = null, AreaStats oldStats = null) { }

        [PatchSessionOrigCtor]
        public extern void orig_ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null);

        [MonoModConstructor]
        public void ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null) {
            patch_AreaData areaData = patch_AreaData.Get(area);
            if (area.Mode == AreaMode.Normal) {
                areaData.RestoreASideAreaData();
            } else {
                areaData.OverrideASideMeta(area.Mode);
            }
            orig_ctor(area, checkpoint, oldStats);
        }
    }
}

namespace MonoMod {

    /// <summary>
    /// Patch the session .ctor to default to "" if the map has no rooms,
    /// or the default room does not exist
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchSessionOrigCtor))]
    class PatchSessionOrigCtorAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchSessionOrigCtor(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt("Celeste.MapData", "StartLevel"));

            ILLabel end = cursor.DefineLabel();

            // go to ldfld string LevelData::Name if StartLevel() is not null
            ILLabel yesLdfldName = cursor.DefineLabel();
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Brtrue_S, yesLdfldName);

            // else replace it with null and skip the ldfld string LevelData::Name
            ILLabel noLdfldName = cursor.DefineLabel();
            cursor.Emit(OpCodes.Pop);
            cursor.Emit(OpCodes.Ldnull);
            cursor.Emit(OpCodes.Br_S, noLdfldName);

            // next opcode is ldfld string LevelData::Name; mark the labels
            cursor.MarkLabel(yesLdfldName);
            cursor.Index++;
            cursor.MarkLabel(noLdfldName);

            // now use the value if it's not null, else default to ""
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Brtrue_S, end);
            cursor.Emit(OpCodes.Pop);
            cursor.Emit(OpCodes.Ldstr, "");

            // don't forget to mark where the stfld string Session::Level is
            cursor.MarkLabel(end);
        }
    }
}
