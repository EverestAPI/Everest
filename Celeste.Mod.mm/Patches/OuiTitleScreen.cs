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
using Celeste.Mod.UI;

namespace Celeste {
    class patch_OuiTitleScreen : OuiTitleScreen {

        // We're effectively in OuiTitleScreen, but still need to "expose" private fields to our mod.
        private string version;
        private float textY;
        private float alpha;
        private Image logo;
        private MTexture title;
        private List<MTexture> reflections;
        private bool hideConfirmButton;

        private float switchingToVanilla;
        private float switchingToVanillaBack;
        private const float switchingToVanillaDuration = 2f;

        private MTexture updateTex;
        private float updateAlpha;
        private bool updateChecked;

        private Image vanillaLogo;
        private MTexture vanillaTitle;
        private List<MTexture> vanillaReflections;

        private Image everestLogo;
        private MTexture everestTitle;
        private List<MTexture> everestReflections;

        private MTexture arrowToVanilla;

        // Patching constructors is ugly.
        public extern void orig_ctor();
        [MonoModConstructor]
        public void ctor() {
            bool fmodLiveUpdate = Settings.Instance.LaunchWithFMODLiveUpdate;
            Settings.Instance.LaunchWithFMODLiveUpdate |= CoreModule.Settings.LaunchWithFMODLiveUpdateInEverest;

            orig_ctor();

            Settings.Instance.LaunchWithFMODLiveUpdate = fmodLiveUpdate;

            vanillaLogo = logo;
            vanillaTitle = title;
            vanillaReflections = reflections;

            if (!Everest.Flags.IsDisabled) {
                everestLogo = new Image(GFX.Gui["logo_everest"]);
                everestLogo.CenterOrigin();
                everestLogo.Position = new Vector2(1920f, 1080f) / 2f;

                everestTitle = GFX.Gui["title_everest"];

                everestReflections = new List<MTexture>();
                for (int i = everestTitle.Height - 4; i > 0; i -= 4)
                    everestReflections.Add(everestTitle.GetSubtexture(0, i, everestTitle.Width, 4, null));

                arrowToVanilla = AppDomain.CurrentDomain.IsDefaultAppDomain() ? null : GFX.Gui["dotarrow"];

                version += $"\nEverest v.{Everest.Version}-{Everest.VersionTag}";
            }

            // Initialize DebugRC here, as the play mode can change during the intro.
            Everest.DebugRC.Initialize();
        }

        public extern bool orig_IsStart(Overworld overworld, Overworld.StartMode start);
        public override bool IsStart(Overworld overworld, Overworld.StartMode start) {
            if (CoreModule.Settings.CurrentVersion == null && !overworld.IsCurrent<OuiOOBE>())
                start = Overworld.StartMode.MainMenu;
            return orig_IsStart(overworld, start);
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
                    if (slot == -1)
                        save.DebugMode = true;
                    if (save.CurrentSession?.InArea ?? false) {
                        LevelEnter.Go(save.CurrentSession, true);
                    } else {
                        Overworld.Goto<OuiChapterSelect>();
                    }
                }
            }

            if (!updateChecked && Everest.Updater.HasUpdate && Everest.Updater.Newest != null && alpha >= 1f) {
                updateChecked = true;
                updateTex = Everest.Updater.Newest.Branch == "stable" ? GFX.Gui["areas/new"] : GFX.Gui["areas/new-yellow"];
                Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 0.3f, true);
                tween.OnUpdate = t => {
                    updateAlpha = t.Percent;
                };
                Add(tween);
            }

            if (alpha >= 1f && Selected && Input.MenuRight && arrowToVanilla != null) {
                switchingToVanillaBack = Math.Max(0f, switchingToVanillaBack - Engine.DeltaTime * 8f);
                switchingToVanilla += Engine.DeltaTime;

                if (switchingToVanilla >= switchingToVanillaDuration && !Everest.RestartVanilla) {
                    Everest.RestartVanilla = true;
                    new FadeWipe(Scene, false, () => {
                        Engine.Scene = new Scene();
                        Engine.Instance.Exit();
                    });
                }

            } else if (switchingToVanilla < switchingToVanillaDuration) {
                if (switchingToVanilla > 0f)
                    switchingToVanillaBack = Math.Max(switchingToVanilla, switchingToVanillaBack);
                switchingToVanillaBack = Math.Max(0f, switchingToVanillaBack - Engine.DeltaTime * 4f);
                switchingToVanilla = 0f;
            }
        }

        public extern void orig_Render();
        public override void Render() {
            if (Everest.Flags.IsDisabled) {
                orig_Render();
                return;
            }

            if (CoreModule.Settings.ShowEverestTitleScreen) {
                logo = everestLogo;
                title = everestTitle;
                reflections = everestReflections;

            } else {
                logo = vanillaLogo;
                title = vanillaTitle;
                reflections = vanillaReflections;
            }

            float alphaPrev = alpha;
            float textYPrev = textY;
            float switchAlpha = Ease.CubeInOut(Calc.Clamp(Math.Max(switchingToVanilla, switchingToVanillaBack), 0f, 1f));
            alpha = Calc.Clamp(alpha - switchAlpha, 0f, 1f);
            textY += switchAlpha * 200f;

            orig_Render();

            arrowToVanilla?.DrawJustified(new Vector2(1920f - 80f + (textY - 1000f) * 2f, 540f), new Vector2(1f, 0.5f), Color.White * alpha);

            updateTex?.DrawJustified(new Vector2(80f - 4f, textY + 8f * (1f - updateAlpha) + 2f), new Vector2(1f, 1f), Color.White * updateAlpha, 0.8f);

            alpha = alphaPrev;
            textY = textYPrev;

            if (switchAlpha > 0f) {
                Draw.Rect(0f, 0f, 1920f, 1080f, Color.Black * switchAlpha);
                float offs = 40f * (1f - switchAlpha);
                ActiveFont.Draw(Dialog.Clean("MENU_TITLESCREEN_RESTART_VANILLA"), new Vector2(960f + offs, 540f - 4f), new Vector2(0.5f, 1f), Vector2.One, Color.White * switchAlpha);
                Draw.Rect(960f - 200f + offs, 540f + 4f, 400f, 4f, Color.Black * switchAlpha * switchAlpha);
                Draw.HollowRect(960f - 200f + offs, 540f + 4f, 400f, 4f, Color.DarkSlateGray * switchAlpha);
                Draw.Rect(960f - 200f + offs, 540f + 4f, 400f * Calc.Clamp(Math.Max(switchingToVanilla, switchingToVanillaBack) / switchingToVanillaDuration, 0f, 1f), 4f, Color.White * switchAlpha);
            }
        }

    }
}
