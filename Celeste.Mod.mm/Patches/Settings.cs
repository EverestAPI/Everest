﻿using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_Settings : Settings {

        [MonoModIgnore]
        [PatchSettingsSetDefaultKeyboardControls]
        public extern new void SetDefaultKeyboardControls(bool reset);

        #region Legacy Input

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Left")]
        [XmlIgnore]
        public Keys Left_V1 {
            get => Left.Keyboard.FirstOrDefault();
            set => Left.Keyboard = new List<Keys> { value };
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Right")]
        [XmlIgnore]
        public Keys Right_V1 {
            get => Right.Keyboard.FirstOrDefault();
            set => Right.Keyboard = new List<Keys> { value };
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Down")]
        [XmlIgnore]
        public Keys Down_V1 {
            get => Down.Keyboard.FirstOrDefault();
            set => Down.Keyboard = new List<Keys> { value };
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Up")]
        [XmlIgnore]
        public Keys Up_V1 {
            get => Up.Keyboard.FirstOrDefault();
            set => Up.Keyboard = new List<Keys> { value };
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Grab")]
        [XmlIgnore]
        public List<Keys> Grab_V1 {
            get => Grab.Keyboard;
            set => Grab.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Jump")]
        [XmlIgnore]
        public List<Keys> Jump_V1 {
            get => Jump.Keyboard;
            set => Jump.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Dash")]
        [XmlIgnore]
        public List<Keys> Dash_V1 {
            get => Dash.Keyboard;
            set => Dash.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Talk")]
        [XmlIgnore]
        public List<Keys> Talk_V1 {
            get => Talk.Keyboard;
            set => Talk.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Pause")]
        [XmlIgnore]
        public List<Keys> Pause_V1 {
            get => Pause.Keyboard;
            set => Pause.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Confirm")]
        [XmlIgnore]
        public List<Keys> Confirm_V1 {
            get => Confirm.Keyboard;
            set => Confirm.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Cancel")]
        [XmlIgnore]
        public List<Keys> Cancel_V1 {
            get => Cancel.Keyboard;
            set => Cancel.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Journal")]
        [XmlIgnore]
        public List<Keys> Journal_V1 {
            get => Journal.Keyboard;
            set => Journal.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::QuickRestart")]
        [XmlIgnore]
        public List<Keys> QuickRestart_V1 {
            get => QuickRestart.Keyboard;
            set => QuickRestart.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnGrab")]
        [XmlIgnore]
        public List<Buttons> BtnGrab {
            get => Grab.Controller;
            set => Grab.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnJump")]
        [XmlIgnore]
        public List<Buttons> BtnJump {
            get => Jump.Controller;
            set => Jump.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnDash")]
        [XmlIgnore]
        public List<Buttons> BtnDash {
            get => Dash.Controller;
            set => Dash.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnTalk")]
        [XmlIgnore]
        public List<Buttons> BtnTalk {
            get => Talk.Controller;
            set => Talk.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnAltQuickRestart")]
        [XmlIgnore]
        public List<Buttons> BtnAltQuickRestart {
            get => QuickRestart.Controller;
            set => QuickRestart.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnDemoDash")]
        [XmlIgnore]
        public List<Buttons> BtnDemoDash {
            get => DemoDash.Controller;
            set => DemoDash.Controller = value;
        }

        // Technically unrelated from the Input V1 / V2 split but these changes were introduced at the same time...

        [XmlIgnore]
        [MonoModLinkFrom("System.Boolean Celeste.Settings::DisableScreenShake")]
        public bool DisableScreenShake {
            get => ScreenShake == ScreenshakeAmount.Off;
            set => ScreenShake = value ? ScreenshakeAmount.Off : ScreenshakeAmount.On;
        }

        #endregion

        [MonoModIgnore]
        public static extern void orig_Initialize();
        public new static void Initialize() {
            orig_Initialize();
            // Load Mouse Button Bindings, which are applied to the already-loaded settings.
            if (UserIO.Open(UserIO.Mode.Read)) {
                (UserIO.Load<VanillaMouseBindings>("modsettings-Everest_MouseBindings") ?? new VanillaMouseBindings().Init()).Apply();
                UserIO.Close();
            }
        }

        public void ClearMouseControls() {
            ((patch_Binding) Left).Mouse.Clear();
            ((patch_Binding) Right).Mouse.Clear();
            ((patch_Binding) Down).Mouse.Clear();
            ((patch_Binding) Up).Mouse.Clear();
            ((patch_Binding) MenuLeft).Mouse.Clear();
            ((patch_Binding) MenuRight).Mouse.Clear();
            ((patch_Binding) MenuDown).Mouse.Clear();
            ((patch_Binding) MenuUp).Mouse.Clear();
            ((patch_Binding) Grab).Mouse.Clear();
            ((patch_Binding) Jump).Mouse.Clear();
            ((patch_Binding) Dash).Mouse.Clear();
            ((patch_Binding) Talk).Mouse.Clear();
            ((patch_Binding) Pause).Mouse.Clear();
            ((patch_Binding) Confirm).Mouse.Clear();
            ((patch_Binding) Cancel).Mouse.Clear();
            ((patch_Binding) Journal).Mouse.Clear();
            ((patch_Binding) QuickRestart).Mouse.Clear();
            ((patch_Binding) DemoDash).Mouse.Clear();
            ((patch_Binding) LeftMoveOnly).Mouse.Clear();
            ((patch_Binding) RightMoveOnly).Mouse.Clear();
            ((patch_Binding) DownMoveOnly).Mouse.Clear();
            ((patch_Binding) UpMoveOnly).Mouse.Clear();
            ((patch_Binding) LeftDashOnly).Mouse.Clear();
            ((patch_Binding) RightDashOnly).Mouse.Clear();
            ((patch_Binding) DownDashOnly).Mouse.Clear();
            ((patch_Binding) UpDashOnly).Mouse.Clear();
        }

        [MonoModIfFlag("RelinkXNA")]
        private static void TranslateKeys(List<Keys> keys) {
            for (int i = 0; i < keys.Count; i++)
                keys[i] = Keyboard.GetKeyFromScancodeEXT(keys[i]);
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches Settings.SetDefaultKeyboardControls to take mouse bindings into account
    /// and ensure that TranslateKeys only gets called when reset = true. Additionaly
    /// adds code missing on XNA to the end of the function.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchSettingsSetDefaultKeyboardControls))]
    class PatchSettingsSetDefaultKeyboardControls : Attribute { }

    static partial class MonoModRules {

        public static void PatchSettingsSetDefaultKeyboardControls(ILContext il, CustomAttribute attrib) {
            // Generic types are a mess
            FieldReference f_Binding_Mouse = il.Module.GetType("Monocle.Binding").FindField("Mouse");
            GenericInstanceType t_List_MouseButtons = (GenericInstanceType) il.Module.ImportReference(f_Binding_Mouse.FieldType);
            MethodReference m_temp = t_List_MouseButtons.Resolve().FindProperty("Count").GetMethod;
            MethodReference m_List_MouseButtons_get_Count = il.Module.ImportReference(
                new MethodReference(
                    m_temp.Name,
                    m_temp.ReturnType) {
                    DeclaringType = t_List_MouseButtons,
                    HasThis = m_temp.HasThis,
                    ExplicitThis = m_temp.ExplicitThis,
                    CallingConvention = m_temp.CallingConvention,
                }
            );

            ILCursor c = new ILCursor(il);
            ILCursor c_Search = c.Clone();

            int n = 0;
            while (c.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt(out MethodReference m) && m.Name == "get_Count")) {
                /*
                Before:
                    if (reset || {Input}.Keyboard.Count <= 0)
                After:
                    if (reset || {Input}.Keyboard.Count + {Input}.Mouse.Count <= 0)
                */
                for (int i = 0; i < 4; i++) {
                    // Instead of changing the offset variable, change the number of instructions by emitting :cosmicline:
                    Instruction instr = c_Search.Goto(c.Index - 3).Prev;
                    object operand = instr.Operand switch {
                        FieldReference f when f.Name == "Keyboard" => f_Binding_Mouse,
                        MethodReference m when m.Name == "get_Count" => m_List_MouseButtons_get_Count,
                        _ => instr.Operand
                    };
                    c.Emit(instr.OpCode, operand);
                }
                c.Emit(OpCodes.Add);
                n++;
            }
            if (n != 17)
                throw new Exception("Incorrect number of matches for get_Count in Settings.SetDefaultKeyboardControls");


            // find the instruction after the group of TranslateKeys.
            while (c_Search.TryGotoNext(MoveType.After, instr => instr.MatchCall("Celeste.Settings", "TranslateKeys"))) { }
            Instruction jumpTarget = c_Search.Next;

            // go just before the first TranslateKeys call.
            if (c.TryGotoNext(MoveType.AfterLabel,
                instr => instr.MatchLdarg(0),
                instr => instr.OpCode == OpCodes.Ldfld,
                instr => instr.OpCode == OpCodes.Ldfld,
                instr => instr.MatchCall("Celeste.Settings", "TranslateKeys"))) {

                // enclose all TranslateKeys inside a if (reset || !Existed).
                c.Emit(OpCodes.Ldarg_1);
                c.Emit(OpCodes.Brtrue, c.Next);
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldfld, il.Method.DeclaringType.FindField("Existed"));
                c.Emit(OpCodes.Brtrue, jumpTarget);
            }

            // Add missing code to the end of XNA versions of the function
            if (!IsRelinkingXNAInstall)
                return;

            c.GotoNext(i => i.MatchRet());
            c.MoveAfterLabels();

            ILLabel checkSuccess = c.DefineLabel(), checkFail = c.DefineLabel();

            c.Emit(OpCodes.Ldarg_1);
            c.Emit(OpCodes.Brtrue_S, checkSuccess);

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldfld, il.Method.DeclaringType.FindField("Existed"));
            c.Emit(OpCodes.Brtrue_S, checkFail);
            c.MarkLabel(checkSuccess);

            MethodReference m_Settings_TranslateKeys = il.Method.DeclaringType.FindMethod("TranslateKeys");
            FieldReference f_Binding_Keyboard = il.Module.GetType("Monocle.Binding").FindField("Keyboard");
            foreach (string bindingName in new string[] {
                "Left", "Right", "Down", "Up", "MenuLeft", "MenuRight", "MenuDown", "MenuUp",
                "Grab", "Jump", "Dash", "Talk", "Pause", "Confirm", "Cancel", "Journal", "QuickRestart", "DemoDash",
                "LeftMoveOnly", "RightMoveOnly", "UpMoveOnly", "DownMoveOnly", "LeftDashOnly", "RightDashOnly", "UpDashOnly", "DownDashOnly"
            }) {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldfld, il.Method.DeclaringType.FindField(bindingName));
                c.Emit(OpCodes.Ldfld, f_Binding_Keyboard);
                c.Emit(OpCodes.Call, m_Settings_TranslateKeys);
            }

            c.MarkLabel(checkFail);
        }

    }
}
