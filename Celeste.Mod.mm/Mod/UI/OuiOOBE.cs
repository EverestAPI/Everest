using Celeste.Mod.Core;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Celeste.TextMenu;

namespace Celeste.Mod.UI {
    public class OuiOOBE : Oui, OuiModOptions.ISubmenu {

        private const float onScreenX = 960f;
        private const float offScreenLeftX = -960f;
        private const float offScreenRightX = 2880f;

        private bool fromModOptions;
        private float fade = 0f;
        private TextMenu menu;
        private Stack<TextMenu> steps = new Stack<TextMenu>();
        private int step = 0;

        public OuiOOBE() {
        }

        public TextMenu CreateMenu(int step) {
            switch (step) {
                // Intro
                case 0:
                    return new TextMenu() {
                        new Header(Dialog.Clean("OOBE_WELCOME_HEADER")),
                        new SubHeader(Dialog.Clean("OOBE_WELCOME_SUBHEADER")),
                        new Button(Dialog.Clean("OOBE_WELCOME_PLAY")).Pressed(Goto(1)),
                        new Button(Dialog.Clean("OOBE_WELCOME_SPEEDRUN")).Pressed(Goto(2)),
                        new Button(Dialog.Clean("OOBE_WELCOME_CREATE")).Pressed(Goto(3)),
                        new Button(Dialog.Clean("OOBE_WELCOME_SKIP")).Pressed(Exit)
                    };


                // Players
                case 1:
                    return new TextMenu() {
                        new SubHeader(Dialog.Clean("OOBE_SETTINGS_PLAY")),
                        new SubHeader(""),

                        new SubHeader(Dialog.Clean("OOBE_SETTINGS_SUBHEADER")),

                        new OnOff(
                            Dialog.Clean("MODOPTIONS_COREMODULE_AUTOUPDATEMODSONSTARTUP"),
                            CoreModule.Settings.AutoUpdateModsOnStartup
                        ).Change(
                            value => CoreModule.Settings.AutoUpdateModsOnStartup = value
                        ),

                        new TextMenu.Slider(
                            Dialog.Clean("MODOPTIONS_COREMODULE_DEBUGMODE"),
                            i => Dialog.Clean($"MODOPTIONS_VANILLATRISTATE_{(CoreModuleSettings.VanillaTristate) i}"),
                            0,
                            Enum.GetValues(typeof(CoreModuleSettings.VanillaTristate)).Length - 1,
                            (int) CoreModule.Settings.DebugMode
                        ).Change(
                            value => CoreModule.Settings.DebugMode = (CoreModuleSettings.VanillaTristate) value
                        ),

                        new OnOff(
                            Dialog.Clean("MODOPTIONS_COREMODULE_SHOWEVERESTTITLESCREEN"),
                            CoreModule.Settings.ShowEverestTitleScreen
                        ).Change(
                            value => CoreModule.Settings.ShowEverestTitleScreen = value
                        ),

                        new SubHeader(Dialog.Clean("OOBE_SETTINGS_MORE")),
                        new Button(Dialog.Clean("OOBE_SETTINGS_OK")).Pressed(Exit)
                    };


                // Speedrunners
                case 2:
                    return new TextMenu() {
                        new SubHeader(Dialog.Clean("OOBE_SETTINGS_SPEEDRUN")),
                        new SubHeader(""),

                        new SubHeader(Dialog.Clean("OOBE_SETTINGS_SUBHEADER")),

                        new OnOff(
                            Dialog.Clean("MODOPTIONS_COREMODULE_LAUNCHWITHOUTINTRO"),
                            CoreModule.Settings.LaunchWithoutIntro
                        ).Change(
                            value => CoreModule.Settings.LaunchWithoutIntro = value
                        ),

                        new TextMenu.Slider(
                            Dialog.Clean("MODOPTIONS_COREMODULE_DEBUGMODE"),
                            i => Dialog.Clean($"MODOPTIONS_VANILLATRISTATE_{(CoreModuleSettings.VanillaTristate) i}"),
                            0,
                            Enum.GetValues(typeof(CoreModuleSettings.VanillaTristate)).Length - 1,
                            (int) CoreModule.Settings.DebugMode
                        ).Change(
                            value => CoreModule.Settings.DebugMode = (CoreModuleSettings.VanillaTristate) value
                        ),

                        new OnOff(
                            Dialog.Clean("MODOPTIONS_COREMODULE_SHOWMODOPTIONSINGAME"),
                            CoreModule.Settings.ShowModOptionsInGame
                        ).Change(
                            value => CoreModule.Settings.ShowModOptionsInGame = value
                        ),

                        new OnOff(
                            Dialog.Clean("MODOPTIONS_COREMODULE_SHOWEVERESTTITLESCREEN"),
                            CoreModule.Settings.ShowEverestTitleScreen
                        ).Change(
                            value => CoreModule.Settings.ShowEverestTitleScreen = value
                        ),

                        new SubHeader(Dialog.Clean("OOBE_SETTINGS_MORE")),
                        new Button(Dialog.Clean("OOBE_SETTINGS_OK")).Pressed(Exit)
                    };


                // Modders / Mappers
                case 3: {
                        Item fmod;
                        TextMenu menu = new TextMenu() {
                            new SubHeader(Dialog.Clean("OOBE_SETTINGS_CREATE")),
                            new SubHeader(""),

                            new SubHeader(Dialog.Clean("OOBE_SETTINGS_SUBHEADER")),

                            new TextMenu.Slider(
                                Dialog.Clean("MODOPTIONS_COREMODULE_DEBUGMODE"),
                                i => Dialog.Clean($"MODOPTIONS_VANILLATRISTATE_{(CoreModuleSettings.VanillaTristate) i}"),
                                0,
                                Enum.GetValues(typeof(CoreModuleSettings.VanillaTristate)).Length - 1,
                                (int) CoreModule.Settings.DebugMode
                            ).Change(
                                value => CoreModule.Settings.DebugMode = (CoreModuleSettings.VanillaTristate) value
                            ),

                            (fmod = new TextMenu.Slider(
                                Dialog.Clean("MODOPTIONS_COREMODULE_LAUNCHWITHFMODLIVEUPDATE"),
                                i => Dialog.Clean($"MODOPTIONS_VANILLATRISTATE_{(CoreModuleSettings.VanillaTristate) i}"),
                                0,
                                Enum.GetValues(typeof(CoreModuleSettings.VanillaTristate)).Length - 1,
                                (int) CoreModule.Settings.LaunchWithFMODLiveUpdate
                            ).Change(
                                value => CoreModule.Settings.LaunchWithFMODLiveUpdate = (CoreModuleSettings.VanillaTristate) value
                            )),

                            new OnOff(
                                Dialog.Clean("MODOPTIONS_COREMODULE_AUTOUPDATEMODSONSTARTUP"),
                                CoreModule.Settings.AutoUpdateModsOnStartup
                            ).Change(
                                value => CoreModule.Settings.AutoUpdateModsOnStartup = value
                            ),

                            new OnOff(
                                Dialog.Clean("MODOPTIONS_COREMODULE_SHOWEVERESTTITLESCREEN"),
                                CoreModule.Settings.ShowEverestTitleScreen
                            ).Change(
                                value => CoreModule.Settings.ShowEverestTitleScreen = value
                            ),

                            new SubHeader(Dialog.Clean("OOBE_SETTINGS_MORE")),
                            new Button(Dialog.Clean("OOBE_SETTINGS_OK")).Pressed(Exit)
                        };

                        fmod.NeedsRelaunch(menu);
                        return menu;
                    }


                // Wat.
                default:
                    return new TextMenu();
            }
        }

        public void Next() {
            new DynData<TextMenu>(menu).Set("oobeStep", step);
            steps.Push(menu);
            Add(new Coroutine(GotoTransition(step + 1)));
        }

        public void Prev() {
            Add(new Coroutine(GotoTransition(steps.Pop(), true)));
        }

        public Action Goto(int target, bool back = false) {
            return () => {
                new DynData<TextMenu>(menu).Set("oobeStep", step);
                steps.Push(menu);
                Add(new Coroutine(GotoTransition(target, back)));
            };
        }

        private IEnumerator GotoTransition(int target, bool back = false) {
            TextMenu menu = CreateMenu(target);
            if (menu.Height > menu.ScrollableMinSize)
                menu.Position.Y = menu.ScrollTargetY;
            new DynData<TextMenu>(menu).Set("oobeStep", target);
            return GotoTransition(menu, back);
        }


        private IEnumerator GotoTransition(TextMenu target, bool back = false) {
            Audio.Play(SFX.ui_main_whoosh_list_in);

            TextMenu menuOld = menu;
            menuOld.Visible = true;
            menuOld.Focused = false;

            step = new DynData<TextMenu>(target).Get<int>("oobeStep");
            menu = target;
            menu.Visible = true;
            menu.Focused = false;
            Scene.Add(menu);

            if (back) {
                for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                    menuOld.X = onScreenX + 1920f * Ease.CubeInOut(p);
                    menu.X = offScreenLeftX + 1920f * Ease.CubeInOut(p);
                    yield return null;
                }

            } else {
                for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                    menuOld.X = onScreenX - 1920f * Ease.CubeInOut(p);
                    menu.X = offScreenRightX - 1920f * Ease.CubeInOut(p);
                    yield return null;
                }
            }

            menuOld.Visible = false;
            menuOld.RemoveSelf();

            menu.Focused = true;
        }

        private void ReloadMenu() {
            if (menu != null)
                Scene.Remove(menu);

            menu = CreateMenu(step);

            if (menu.Height > menu.ScrollableMinSize)
                menu.Position.Y = menu.ScrollTargetY;

            Scene.Add(menu);
        }

        public override IEnumerator Enter(Oui from) {
            Overworld.ShowInputUI = true;
            fromModOptions = from is OuiModOptions;

            if (fromModOptions)
                Add(new Coroutine(FadeBgTo(1f)));
            else
                fade = 1f;

            steps.Clear();
            step = 0;
            ReloadMenu();

            menu.Visible = Visible = true;
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenRightX - 1920f * Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play(SFX.ui_main_whoosh_large_out);
            menu.Focused = false;

            if (fromModOptions)
                Add(new Coroutine(FadeBgTo(0f)));

            yield return Everest.SaveSettings();

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX - 1920f * Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;

            Overworld.Maddy.Hide();
        }

        private IEnumerator FadeBgTo(float to) {
            while (fade != to) {
                yield return null;
                fade = Calc.Approach(fade, to, Engine.DeltaTime * 2f);
            }
        }

        public override bool IsStart(Overworld overworld, Overworld.StartMode start) {
            if (start != Overworld.StartMode.Titlescreen || CoreModule.Settings.CurrentVersion != null)
                return false;
            Add(new Coroutine(Enter(null)));
            return true;
        }

        public override void Update() {
            if (menu != null && menu.Focused &&
                Selected && Input.MenuCancel.Pressed) {
                if (steps.Count > 0) {
                    Audio.Play(SFX.ui_main_button_back);
                    Prev();
                } else if (fromModOptions) {
                    Overworld.Goto<OuiModOptions>();
                }
            }

            base.Update();
        }

        public override void Render() {
            Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * fade);

            base.Render();
        }

        public void Exit() {
            if (fromModOptions)
                Overworld.Goto<OuiModOptions>();
            else
                _GotoTitleScreen();
        }

        private void _GotoTitleScreen() {
            OuiTitleScreen title = Overworld.Goto<OuiTitleScreen>();
            title.IsStart(Overworld, Overworld.StartMode.Titlescreen);
            title.IsStart(Overworld, Overworld.StartMode.MainMenu);

            DynData<OuiTitleScreen> data = new DynData<OuiTitleScreen>(title);
            data.Set<float>("alpha", 0);
            title.Visible = true;
        }

    }
}
