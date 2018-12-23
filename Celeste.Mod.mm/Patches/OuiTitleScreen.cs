#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Celeste.Mod.Core;

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
        public extern void orig_ctor();
        [MonoModConstructor]
        public void ctor() {
            orig_ctor();

            if (!Everest.Flags.Disabled)
                version += $"\nEverest v.{Everest.Version}-{Everest.VersionTag}";

            updateTex = GFX.Gui["areas/new"];

            // Initialize DebugRC here, as the play mode can change during the intro.
            Everest.DebugRC.Initialize();
        }

        public extern void orig_Update();
        public override void Update() {
            orig_Update();

            // Slightly dirty place to perform this, but oh well...
            if (CoreModule.Settings.QuickRestart != null) {
                int slot = CoreModule.Settings.QuickRestart.Value;
                CoreModule.Settings.QuickRestart = null;
                CoreModule.Instance.SaveSettings();
                SaveData save = UserIO.Load<SaveData>(SaveData.GetFilename(slot));
                if (save != null) {
                    SaveData.Start(save, slot);
                    if (slot == 4)
                        save.DebugMode = true;
                    if (save.CurrentSession?.InArea ?? false) {
                        Engine.Scene = new LevelLoader(save.CurrentSession);
                    } else {
                        Overworld.Goto<OuiChapterSelect>();
                    }
                }
            }

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
