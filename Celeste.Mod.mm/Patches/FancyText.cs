#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    // The FancyText ctor is private, so this cannot inherit from it.
    class patch_FancyText {

        // helper class because manipulating generic types in IL is annoying
        internal class ListChar {
            public List<FancyText.Char> elements = new();
            public static void Add(FancyText.Char c, ListChar self) => self.elements.Add(c);
        }

        [PatchTextIteration]
        [PatchFancyTextAddWord]
        private extern void orig_AddWord(string word);
        private void AddWord(string word) {
            word = Emoji.Apply(word);
            orig_AddWord(word);
        }

        public class patch_Char : FancyText.Char {
            public extern void orig_Draw(PixelFont font, float baseSize, Vector2 position, Vector2 scale, float alpha);
            public new void Draw(PixelFont font, float baseSize, Vector2 position, Vector2 scale, float alpha) {
                Color prevColor = Color;

                if (Emoji.Start <= Character &&
                    Character <= Emoji.Last &&
                    !Emoji.IsMonochrome((char) Character)) {
                    Color = new Color(Color.A, Color.A, Color.A, Color.A);
                }

                orig_Draw(font, baseSize, position, scale, alpha);

                Color = prevColor;
            }
        }

        [MonoModIgnore]  // We don't want to change anything about the method...
        [PatchFancyTextParse]  // ... except for manually manipulating the method via MonoModRules
        private extern FancyText.Text Parse();

        private void ParseCustomCommand(string command, List<string> args, Stack<Color> colorStack, FancyText.Portrait[] lastPortrait) {
            if (Everest.Events.FancyText.ParseCustomCommand((FancyText)(object)this, command, args, colorStack, lastPortrait))
                return;

            Logger.Warn("EventTrigger", $"FancyText command '{command}' does not exist!");
        }

        private void BeforeParse() => Everest.Events.FancyText.BeforeParse((FancyText)(object)this);
        private void AfterParse() => Everest.Events.FancyText.AfterParse((FancyText)(object)this);

        private void WordAdded(string word, UnicodeStringHelper.ListInt codepoints, ListChar chars)
            => Everest.Events.FancyText.WordAdded((FancyText)(object)this, word, codepoints.Elements, chars.elements);

        public extern void orig_Draw(Vector2 position, Vector2 justify, Vector2 scale, float alpha, int start, int end);
        public void Draw(Vector2 position, Vector2 justify, Vector2 scale, float alpha, int start = 0, int end = int.MaxValue) {
            Everest.Events.FancyText.BeforeDraw((FancyText)(object)this, position, justify, scale, alpha, start, end);
            orig_Draw(position, justify, scale, alpha, start, end);
            Everest.Events.FancyText.AfterDraw((FancyText)(object)this, position, justify, scale, alpha, start, end);
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// A patch for FancyText parsing, allowing mods to register custom commands.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFancyTextParse))]
    class PatchFancyTextParse : Attribute { }

    /// <summary>
    /// A patch for FancyText word-adding, allowing mods to manipulate each word.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFancyTextAddWord))]
    class PatchFancyTextAddWord : Attribute { }

    static partial class MonoModRules {

        public static void PatchFancyTextParse(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_FancyText = MonoModRule.Modder.FindType("Celeste.FancyText").Resolve();
            MethodDefinition m_BeforeParse = t_FancyText.FindMethod("BeforeParse");
            MethodDefinition m_FancyText_ParseCustomCommand = t_FancyText.FindMethod("ParseCustomCommand");
            MethodDefinition m_AfterParse = t_FancyText.FindMethod("AfterParse");

            VariableDefinition v_command = context.Body.Variables.First(v => v.VariableType.FullName == "System.String");
            VariableDefinition v_args = context.Body.Variables.First(v => v.VariableType.FullName == "System.Collections.Generic.List`1<System.String>");
            VariableDefinition v_colorStack = context.Body.Variables.First(v => v.VariableType.FullName.StartsWith("System.Collections.Generic.Stack`1<Microsoft.Xna.Framework.Color>"));
            VariableDefinition v_lastPortrait = context.Body.Variables.First(v => v.VariableType.FullName == "Celeste.FancyText/Portrait[]");

            ILCursor cursor = new ILCursor(context);

            // + ldarg.0
            // + call Events.FancyText.BeforeParse

            cursor.EmitLdarg0();
            cursor.EmitCall(m_BeforeParse);

            //   ldstr "savedata"
            //   callvirt System.Boolean System.String::Equals(System.String)
            // - brfalse continue
            //
            // + brtrue savedata
            // + ldarg.0
            // + ldloc.7 (command)
            // + ldloc.8 (args)
            // + ldloc.3 (stack)
            // + ldloc.4 (lastPortrait)
            // + call FancyText.ParseCustomCommand(this, command, args, colorStack, lastPortrait)
            // + br continue
            //
            // + savedata:
            //   (code for handling {savedata})
            //   continue:

            ILLabel label_continue = null;
            ILLabel label_savedata = cursor.DefineLabel();

            cursor.GotoNext(MoveType.After,
                            instr => instr.MatchLdstr("savedata"),
                            instr => instr.MatchCallOrCallvirt(out var _),
                            instr => instr.MatchBrfalse(out label_continue));

            cursor.Index--;
            cursor.Remove();

            cursor.EmitBrtrue(label_savedata);
            cursor.EmitLdarg0();
            cursor.EmitLdloc(v_command);
            cursor.EmitLdloc(v_args);
            cursor.EmitLdloc(v_colorStack);
            cursor.EmitLdloc(v_lastPortrait);
            cursor.EmitCall(m_FancyText_ParseCustomCommand);
            cursor.EmitBr(label_continue);

            cursor.MarkLabel(label_savedata);

            // + ldarg.0
            // + call Events.FancyText.AfterParse
            //   ldarg.0
            //   ldfld FancyText::group
            //   ret

            cursor.GotoNext(MoveType.Before,
                            instr => instr.MatchLdarg0(),
                            instr => instr.MatchLdfld(out var _),
                            instr => instr.MatchRet());

            cursor.EmitLdarg0();
            cursor.EmitCall(m_AfterParse);
        }

        public static void PatchFancyTextAddWord(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_FancyText = MonoModRule.Modder.FindType("Celeste.FancyText").Resolve();
            TypeDefinition t_ListChar = t_FancyText.NestedTypes.First(t => t.Name == "ListChar");

            MethodDefinition m_FancyText_WordAdded = t_FancyText.FindMethod("WordAdded");
            MethodDefinition m_ListChar_ctor = t_ListChar.FindMethod(".ctor", true);
            MethodDefinition m_ListChar_add = t_ListChar.FindMethod("Add");

            VariableDefinition v_listOfCodePoints = context.Body.Variables.First(v => v.VariableType.FullName == "Celeste.Mod.Helpers.UnicodeStringHelper/ListInt");
            VariableDefinition v_chars = new VariableDefinition(t_ListChar);
            context.Body.Variables.Add(v_chars);

            ILCursor cursor = new ILCursor(context);

            // ... (creating the codepoint list)
            // + newobj FancyText/ListChar
            // + stloc.7 (chars)

            cursor.GotoNext(MoveType.After,
                            instr => instr.MatchStloc(out int loc) && loc == v_listOfCodePoints.Index);

            cursor.EmitNewobj(m_ListChar_ctor);
            cursor.EmitStloc(v_chars);

            // ... (creating this Char)
            // + dup
            // + ldloc.7 (chars)
            // + call FancyText/ListChar::Add
            //   callvirt List<FancyText/Node>::Add

            cursor.GotoNext(MoveType.Before,
                            instr => instr.MatchCallOrCallvirt(out var method)
                                  && method.DeclaringType.FullName == "System.Collections.Generic.List`1<Celeste.FancyText/Node>"
                                  && method.Name == "Add");

            cursor.EmitDup();
            cursor.EmitLdloc(v_chars);
            cursor.EmitCall(m_ListChar_add);

            // + ldarg.0
            // + ldarg.1 (word)
            // + ldloc.6 (listOfCodePoints)
            // + ldloc.7 (chars)
            // + call FancyText/WordAdded
            //   ret

            cursor.GotoNext(MoveType.Before,
                            instr => instr.MatchRet());

            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitLdloc(v_listOfCodePoints);
            cursor.EmitLdloc(v_chars);
            cursor.EmitCall(m_FancyText_WordAdded);
        }
    }
}
