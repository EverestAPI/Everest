#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;
using System.IO;
using FMOD.Studio;
using Monocle;
using Celeste.Mod.Meta;

namespace Celeste {
    class patch_MountainRenderer : MountainRenderer {

        [MonoModIgnore]
        public new int Area { get; private set; }
        [MonoModIgnore]
        private bool inFreeCameraDebugMode;

        public float EaseCamera(int area, MountainCamera transform, float? duration = null, bool nearTarget = true) {
            return EaseCamera(area, transform, duration, nearTarget, false);
        }

        public extern void orig_Update(Scene scene);
        public override void Update(Scene scene) {
            AreaData area = -1 < Area && Area < (AreaData.Areas?.Count ?? 0) ? AreaData.Get(Area) : null;
            MapMeta meta = area?.GetMeta();

            if (meta?.Mountain?.ShowCore ?? false) {
                Area = 9;
                orig_Update(scene);
                Area = area.ID;

            } else {
                orig_Update(scene);
            }
        }

        public void SetFreeCam(bool value) {
            inFreeCameraDebugMode = value;
        }

    }
}
