#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OuiTitleScreen : OuiTitleScreen {

        // We're effectively in OuiTitleScreen, but still need to "expose" private fields to our mod.
        private string version;
        private float textY;
        private float alpha;

        private MTexture updateTex;
        private float updateAlpha;
        private bool updateChecked;

        // Patching constructors is ugly.
        public extern void orig_ctor_OuiTitleScreen();
        [MonoModConstructor]
        public void ctor_OuiTitleScreen() {
            orig_ctor_OuiTitleScreen();

            version += $"\nEverest v.{Everest.Version}-{Everest.VersionTag}";
            updateTex = GFX.Gui["areas/new"];

            // Initialize DebugRC here, as the play mode can change during the intro.
            Everest.DebugRC.Initialize();
        }

        public extern void orig_Update();
        public override void Update() {
            orig_Update();

            if (!updateChecked && Everest.Updater.HasUpdate && alpha >= 1f) {
                updateChecked = true;
                Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 0.3f, true);
                tween.OnUpdate = t => {
                    updateAlpha = t.Percent;
                };
                Add(tween);
            }
        }

        public extern void orig_Render();
        public override void Render() {
            orig_Render();
            updateTex.DrawJustified(new Vector2(80f - 4f, textY + 8f * (1f - updateAlpha) + 2f), new Vector2(1f, 1f), Color.White * updateAlpha, 0.8f);
        }

    }
}
