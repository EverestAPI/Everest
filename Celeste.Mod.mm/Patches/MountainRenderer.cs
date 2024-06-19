#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using Celeste.Mod.Meta;
using Celeste.Mod.UI;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_MountainRenderer : MountainRenderer {

        [MonoModIgnore]
        public new int Area { get; private set; }

        private bool inFreeCameraDebugMode;

        public float EaseCamera(int area, MountainCamera transform, float? duration = null, bool nearTarget = true) {
            return EaseCamera(area, transform, duration, nearTarget, false);
        }

        [PatchMountainRendererUpdate]
        public extern void orig_Update(Scene scene);
        public override void Update(Scene scene) {
            patch_AreaData area = -1 < Area && Area < (AreaData.Areas?.Count ?? 0) ? patch_AreaData.Get(Area) : null;
            MapMeta meta = area?.Meta;

            bool wasFreeCam = inFreeCameraDebugMode;

            if (meta?.Mountain?.ShowCore ?? false) {
                Area = 9;
                orig_Update(scene);
                Area = area.ID;

            } else {
                orig_Update(scene);
            }

            Overworld overworld = scene as Overworld;
            if (!wasFreeCam && inFreeCameraDebugMode && (
                ((overworld.Current ?? overworld.Next) is patch_OuiFileNaming naming && naming.UseKeyboardInput) ||
                ((overworld.Current ?? overworld.Next) is OuiModOptionString stringInput && stringInput.UseKeyboardInput))) {

                // we turned on free cam mode (by pressing Space) while on an text entry screen using keyboard input... we should turn it back off.
                inFreeCameraDebugMode = false;
            }
        }

        public void SetFreeCam(bool value) {
            inFreeCameraDebugMode = value;
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Replaces hard-coded key checks with Everest CoreModule ButtonBinding checks
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchMountainRendererUpdate))]
    class PatchMountainRendererUpdate : Attribute { }

    static partial class MonoModRules {

        public static void PatchMountainRendererUpdate(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);

            MethodReference m_Everest_CoreModule_Settings = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModule").FindProperty("Settings").GetMethod;
            TypeDefinition t_Everest_CoreModuleSettings = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModuleSettings");

            MethodReference m_ButtonBinding_Check = MonoModRule.Modder.Module.GetType("Celeste.Mod.ButtonBinding").FindProperty("Check").GetMethod;
            MethodReference m_ButtonBinding_Pressed = MonoModRule.Modder.Module.GetType("Celeste.Mod.ButtonBinding").FindProperty("Pressed").GetMethod;

            var checkKeys = new Dictionary<int, string>() {
                {87 /* Keys.W */, "CameraForward"},
                {83 /* Keys.S */, "CameraBackward"},
                {68 /* Keys.D */, "CameraRight"},
                {65 /* Keys.A */, "CameraLeft"},
                {81 /* Keys.Q */, "CameraUp"},
                {90 /* Keys.Z */, "CameraDown"},
                {160 /* Keys.LeftShift */, "CameraSlow"},
            };

            int key = 0;
            while (cursor.TryGotoNext(MoveType.AfterLabel,
                instr => instr.MatchCall("Monocle.MInput", "get_Keyboard"),
                instr => instr.MatchLdcI4(out key),
                instr => instr.MatchCallvirt("Monocle.MInput/KeyboardData", "Check"))) {
                cursor.Emit(OpCodes.Call, m_Everest_CoreModule_Settings);
                cursor.Emit(OpCodes.Call, t_Everest_CoreModuleSettings.FindProperty(checkKeys[key]).GetMethod);
                cursor.Emit(OpCodes.Call, m_ButtonBinding_Check);

                cursor.RemoveRange(3);
                checkKeys.Remove(key);
            }
            if (checkKeys.Count > 0) {
                throw new Exception("MountainRenderer failed to patch key checks for keys: " + checkKeys.Keys);
            }

            var pressedKeys = new Dictionary<int, string>() {
                {80 /* Keys.P */, "CameraPrint"},
                // { 113 /* Keys.F2 */, "ReloadOverworld" },
                {0x20 /* Keys.Space */, "ToggleMountainFreeCam"},
                // { 112 /* Keys.F1 */, "ReloadMountainViews" },
            };

            while (cursor.TryGotoNext(MoveType.AfterLabel,
                instr => instr.MatchCall("Monocle.MInput", "get_Keyboard"),
                instr => instr.MatchLdcI4(out key),
                instr => instr.MatchCallvirt("Monocle.MInput/KeyboardData", "Pressed"))) {
                // Only some pressed keys are currently handled
                if (!pressedKeys.ContainsKey(key)) {
                    continue;
                }

                cursor.Emit(OpCodes.Call, m_Everest_CoreModule_Settings);
                cursor.Emit(OpCodes.Call, t_Everest_CoreModuleSettings.FindProperty(pressedKeys[key]).GetMethod);
                cursor.Emit(OpCodes.Call, m_ButtonBinding_Pressed);

                cursor.RemoveRange(3);
                pressedKeys.Remove(key);
            }
            if (pressedKeys.Count > 0) {
                throw new Exception("MountainRenderer failed to patch key presses for keys: " + pressedKeys.Keys);
            }
        }

    }
}
