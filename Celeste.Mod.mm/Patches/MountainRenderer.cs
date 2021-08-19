#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod.Meta;
using Celeste.Mod.UI;
using Monocle;
using MonoMod;

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
            AreaData area = -1 < Area && Area < (AreaData.Areas?.Count ?? 0) ? AreaData.Get(Area) : null;
            MapMeta meta = area?.GetMeta();

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
