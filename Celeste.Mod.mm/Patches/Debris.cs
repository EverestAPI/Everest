#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;

namespace Celeste {
    class patch_Debris : Debris {

        private Image image;

        public Debris Init(Vector2 pos, char tileset) {
            return Init(pos, tileset, true);
        }

        public extern Debris orig_Init(Vector2 pos, char tileset, bool playSound = true);
        public new Debris Init(Vector2 pos, char tileset, bool playSound) {
            patch_Debris debris = (patch_Debris) orig_Init(pos, tileset, playSound);

            if (((patch_Autotiler) GFX.FGAutotiler).TryGetCustomDebris(out string path, tileset)) {
                List<MTexture> textures = GFX.Game.GetAtlasSubtextures("debris/" + path);
                debris.image.Texture = Calc.Choose(Calc.Random, textures);
            }

            return debris;
        }

        [MonoModIgnore]
        [PatchDebrisImpactSfx]
        private extern void ImpactSfx(float spd);

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches Celeste.Debris.ImpactSfx(System.Single) to play a custom sound event if defined.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDebrisImpactSfx))]
    class PatchDebrisImpactSfxAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchDebrisImpactSfx(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);

            FieldDefinition f_tileset = context.Method.DeclaringType.FindField("tileset");

            FieldDefinition f_GFX_FGAutotiler = context.Module.GetType("Celeste.GFX").FindField("FGAutotiler");
            MethodDefinition m_Autotiler_TryGetCustomDebrisImpactSfx = context.Module.GetType("Celeste.Autotiler").FindMethod("TryGetCustomDebrisImpactSfx");

            /*
             Change:
             
             string path = "event:/game/general/debris_dirt";
             [...] // tileset checks
             Audio.Play(path, [...]);
             
             to:
             
             if (!GFX.FGAutotiler.TryGetCustomDebrisImpactSfx(out string path, tileset)) {
                 path = "event:/game/general/debris_dirt";
                 [...] // tileset checks
             }
             Audio.Play(path, [...]);
             */

            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr("event:/game/general/debris_dirt"));

            cursor.Emit(OpCodes.Ldsfld, f_GFX_FGAutotiler);
            cursor.Emit(OpCodes.Ldloca_S, (byte) 0);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_tileset);
            cursor.Emit(OpCodes.Callvirt, m_Autotiler_TryGetCustomDebrisImpactSfx);

            ILLabel playAudioLabel = null;
            cursor.Clone().GotoNext(instr => instr.MatchBr(out playAudioLabel));

            cursor.Emit(OpCodes.Brtrue_S, playAudioLabel);
        }

    }
}
