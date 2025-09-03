#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;

namespace Monocle {
    class patch_PixelFontSize : PixelFontSize {

        [PatchTextIteration]
        public extern Vector2 orig_Measure(string text);
        public new Vector2 Measure(string text) {
            text = Emoji.Apply(text);
            return orig_Measure(text);
        }

        [MonoModIgnore]
        [PatchTextIteration]
        public extern new float WidthToNextLine(string text, int start);

        public extern void orig_Draw(char character, Vector2 position, Vector2 justify, Vector2 scale, Color color);
        public new void Draw(char character, Vector2 position, Vector2 justify, Vector2 scale, Color color) {
            if (Emoji.Start <= character &&
                character <= Emoji.Last &&
                !Emoji.IsMonochrome(character)) {
                color = new Color(color.A, color.A, color.A, color.A);
            }
            orig_Draw(character, position, justify, scale, color);
        }

        [MonoModReplace]
        public new void Draw(string text, Vector2 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke, Color strokeColor) {
            text = Emoji.Apply(text);

            if (string.IsNullOrEmpty(text))
                return;

            Vector2 offset = Vector2.Zero;
            Vector2 justifyOffs = new Vector2(
                ((justify.X != 0f) ? WidthToNextLine(text, 0) : 0f) * justify.X,
                HeightOf(text) * justify.Y
            );

            UnicodeStringHelper.ListInt codePoints = UnicodeStringHelper.ToCodePointList(text);
            for (int i = 0; i < codePoints.Count; i++) {
                if (codePoints[i] == '\n') {
                    offset.X = 0f;
                    offset.Y += LineHeight;
                    if (justify.X != 0f)
                        justifyOffs.X = WidthToNextLine(text, i + 1) * justify.X;
                    continue;
                }

                PixelFontCharacter c = null;
                if (!Characters.TryGetValue(codePoints[i], out c))
                    continue;

                Vector2 pos = position + (offset + new Vector2(c.XOffset, c.YOffset) - justifyOffs) * scale;
                if (stroke > 0f && !Outline) {
                    if (edgeDepth > 0f) {
                        c.Texture.Draw(pos + new Vector2(0f, -stroke), Vector2.Zero, strokeColor, scale);
                        for (float num2 = -stroke; num2 < edgeDepth + stroke; num2 += stroke) {
                            c.Texture.Draw(pos + new Vector2(-stroke, num2), Vector2.Zero, strokeColor, scale);
                            c.Texture.Draw(pos + new Vector2(stroke, num2), Vector2.Zero, strokeColor, scale);
                        }
                        c.Texture.Draw(pos + new Vector2(-stroke, edgeDepth + stroke), Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(0f, edgeDepth + stroke), Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(stroke, edgeDepth + stroke), Vector2.Zero, strokeColor, scale);
                    } else {
                        c.Texture.Draw(pos + new Vector2(-1f, -1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(0f, -1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(1f, -1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(-1f, 0f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(1f, 0f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(-1f, 1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(0f, 1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(1f, 1f) * stroke, Vector2.Zero, strokeColor, scale);
                    }
                }

                if (edgeDepth > 0f)
                    c.Texture.Draw(pos + Vector2.UnitY * edgeDepth, Vector2.Zero, edgeColor, scale);

                Color cColor = color;
                if (Emoji.Start <= c.Character &&
                    c.Character <= Emoji.Last &&
                    !Emoji.IsMonochrome((char) c.Character)) {
                    cColor = new Color(color.A, color.A, color.A, color.A);
                }
                c.Texture.Draw(pos, Vector2.Zero, cColor, scale);

                offset.X += c.XAdvance;

                if (i < codePoints.Count - 1 && c.Kerning.TryGetValue(codePoints[i + 1], out int kerning)) {
                    offset.X += kerning;
                }
            }
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Replace the char-by-char text iteration with a codepoint-per-codepoint one.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchTextIteration))]
    class PatchTextIterationAttribute : Attribute {
    }

    static partial class MonoModRules {
        private static void emitCallToCodePointList(ILCursor cursor, VariableDefinition v_listOfCodePoints) {
            // List<int> listOfCodePoints = UnicodeStringHelper.ToCodePointList(text);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Call, MonoModRule.Modder.FindType("Celeste.Mod.Helpers.UnicodeStringHelper").Resolve().FindMethod("ToCodePointList"));
            cursor.Emit(OpCodes.Stloc, v_listOfCodePoints);
        }

        public static void PatchTextIteration(ILContext context, CustomAttribute attrib) {
            TypeReference t_List_int = MonoModRule.Modder.FindType("Celeste.Mod.Helpers.UnicodeStringHelper").Resolve().NestedTypes[0];
            VariableDefinition v_listOfCodePoints = new VariableDefinition(t_List_int);
            context.Body.Variables.Add(v_listOfCodePoints);

            ILCursor cursor = new ILCursor(context);
            if (context.Method.Name == "orig_AddWord") {
                // start after the PixelFontSize.Measure call because it needs to get the original text, not the code point thingy
                cursor.GotoNext(MoveType.After, instr => instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.Name == "Measure");

                emitCallToCodePointList(cursor, v_listOfCodePoints);
            } else {
                // jump after the initial if (string.IsNullOrEmpty) { return; }
                cursor.GotoNext(MoveType.After, instr => instr.MatchRet());

                emitCallToCodePointList(cursor, v_listOfCodePoints);

                // make sure the call to ToCodePointList is outside the if (string.IsNullOrEmpty) { ... }
                ILCursor branch = cursor.Clone().GotoPrev(instr => instr.OpCode == OpCodes.Brfalse_S);
                branch.Next.Operand = cursor.Previous.Previous.Previous;
            }

            // text => listOfCodePoints
            int index = cursor.Index;
            while (cursor.TryGotoNext(instr => instr.MatchLdarg1())) {
                cursor.Next.OpCode = OpCodes.Ldloc;
                cursor.Next.Operand = v_listOfCodePoints;
            }

            // text.Length => listOfCodePoints.Count
            cursor.Index = index;
            while (cursor.TryGotoNext(instr => instr.MatchCallvirt<string>("get_Length"))) {
                cursor.Next.OpCode = OpCodes.Call;
                cursor.Next.Operand = t_List_int.Resolve().FindMethod("get_Count");
            }

            // text[i] => listOfCodePoints[i]
            cursor.Index = index;
            while (cursor.TryGotoNext(instr => instr.MatchCallvirt<string>("get_Chars"))) {
                cursor.Next.OpCode = OpCodes.Call;
                cursor.Next.Operand = t_List_int.Resolve().FindMethod("get_Item");
            }
        }
    }
}
