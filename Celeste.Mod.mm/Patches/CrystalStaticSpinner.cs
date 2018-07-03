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
    class patch_CrystalStaticSpinner : CrystalStaticSpinner {

        private CrystalColor color;

        public patch_CrystalStaticSpinner(Vector2 position, bool attachToSolid, CrystalColor color)
            : base(position, attachToSolid, color) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Awake(Scene scene);
        public override void Awake(Scene scene) {
            if ((int) color == -1) {
                Add(new CoreModeListener(this));
                if ((scene as Level).CoreMode == Session.CoreModes.Cold) {
                    color = CrystalColor.Blue;
                } else {
                    color = CrystalColor.Red;
                }
            }

            orig_Awake(scene);
        }

        [MonoModIgnore]
        private class CoreModeListener : Component {
            public CoreModeListener(CrystalStaticSpinner parent)
                : base(true, false) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }
        }

    }
}
