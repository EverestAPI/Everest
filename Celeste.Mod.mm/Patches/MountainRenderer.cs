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

        // Celeste 1.2.6.X comes with a new parameter.

        [MonoModLinkTo("Celeste.MountainRenderer", "System.Single EaseCamera(System.Int32,Celeste.MountainCamera,System.Nullable`1<System.Single>,System.Boolean)")]
        [MonoModIgnore]
        public extern float EaseCameraOld(int area, MountainCamera transform, float? duration = null, bool nearTarget = true);

        [MonoModIfFlag("V1:EaseCamera")]
        [MonoModLinkFrom("System.Single Celeste.MountainRenderer::EaseCamera(System.Int32,Celeste.MountainCamera,System.Nullable`1<System.Single>,System.Boolean,System.Boolean)")]
        public float EaseCameraShim(int area, MountainCamera transform, float? duration = null, bool nearTarget = true, bool targetRotate = false) {
            return EaseCameraOld(area, transform, duration, nearTarget);
        }

        [MonoModIfFlag("V2:EaseCamera")]
        [MonoModLinkFrom("System.Single Celeste.MountainRenderer::EaseCamera(System.Int32,Celeste.MountainCamera,System.Nullable`1<System.Single>,System.Boolean)")]
        public float EaseCameraShim(int area, MountainCamera transform, float? duration = null, bool nearTarget = true) {
            return EaseCamera(area, transform, duration, nearTarget);
        }

    }
}
