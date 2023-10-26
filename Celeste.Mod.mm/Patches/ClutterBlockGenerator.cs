#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;

namespace Celeste {
    // ClutterBlockGenerator is static, so we cannot extend it.
    public static class patch_ClutterBlockGenerator {

        // expose this private struct and field to our mod.
        private struct Tile { }

        private static Tile[,] tiles;
        private static bool initialized;

        public static extern void orig_Init(Level lvl);
        public static void Init(Level lvl) {
            // like vanilla, skip everything if the generator was already initialized.
            if (initialized) {
                return;
            }

            // like vanilla, initialize the tile array at 200x200 size if not initialized yet.
            if (tiles == null) {
                tiles = new Tile[200, 200];
            }

            // check that the tile array is big enough
            int neededWidth = lvl.Bounds.Width / 8;
            int neededHeight = lvl.Bounds.Height / 8 + 1;
            if (tiles.GetLength(0) < neededWidth || tiles.GetLength(1) < neededHeight) {
                // if not, extend it as required.
                int newWidth = Math.Max(tiles.GetLength(0), neededWidth);
                int newHeight = Math.Max(tiles.GetLength(1), neededHeight);

                tiles = new Tile[newWidth, newHeight];
            }

            // carry on with vanilla.
            orig_Init(lvl);
        }

        [MonoModIgnore]
        [PatchClutterBlockGeneratorAdd]
        public static extern void Add(int x, int y, int w, int h, ClutterBlock.Colors color);
    }
}

namespace MonoMod {
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchClutterBlockGeneratorAdd))]
    class PatchClutterBlockGeneratorAddAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchClutterBlockGeneratorAdd(ILContext context, CustomAttribute attrib) {
            FieldReference f_temporaryEntityData = MonoModRule.Modder.Module.GetType("Celeste.Level").Resolve().FindField("temporaryEntityData");
            MethodDefinition m_RegisterEntityDataWithEntity = MonoModRule.Modder.Module.GetType("Celeste.Level").FindMethod("Monocle.Entity RegisterEntityDataWithEntity(Monocle.Entity,Celeste.EntityData)");


            ILCursor cursor = new ILCursor(context);
            // level.Add(<entityStuff>) => level.Add(RegisterEntityDataWithEntity(<entityStuff>))
            cursor.GotoNext(i => i.OpCode == OpCodes.Callvirt && i.Operand is MethodReference mr && mr.FullName == "System.Void Monocle.Scene::Add(Monocle.Entity)");
            cursor.Emit(OpCodes.Ldsfld, f_temporaryEntityData);
            cursor.Emit(OpCodes.Call, m_RegisterEntityDataWithEntity);
        }
    }
}
