using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Mod.UI {
    /// <summary>
    /// A generic menu screen, showing options.
    /// </summary>
    public abstract class OuiGenericMenu : Oui {

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        /// <summary>
        /// The text menu this screen contains.
        /// </summary>
        protected patch_TextMenu menu;

        /// <summary>
        /// The title for the menu.
        /// </summary>
        public abstract string MenuName { get; }

        /// <summary>
        /// Optional parameters this menu can take.
        /// </summary>
        protected object[] parameters;

        /// <summary>
        /// An action executed when the player presses Back.
        /// </summary>
        protected Action<Overworld> backToParentMenu;

        /// <summary>
        /// Whether the player can go back to the parent menu.
        /// </summary>
        protected bool canGoBack = true;

        private float alpha = 0f;

        /// <summary>
        /// Adds all the submenu options to the TextMenu given in parameter.
        /// </summary>
        protected abstract void addOptionsToMenu(patch_TextMenu menu);

        public override IEnumerator Enter(Oui from) {
            // build the menu
            menu = new patch_TextMenu();
            menu.Add(new TextMenu.Header(MenuName));
            addOptionsToMenu(menu);

            // add it to the scene
            Scene.Add(menu);
            menu.Visible = Visible = true;
            menu.Focused = false;

            // transition in
            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            // give control to the player
            menu.Focused = true;
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play(SFX.ui_main_whoosh_large_out);

            // take control from the player
            menu.Focused = false;

            // transition out
            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            // destroy the menu
            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;
        }

        public override void Update() {
            if (menu != null && menu.Focused && Selected && canGoBack && Input.MenuCancel.Pressed) {
                // Back was pressed
                Audio.Play(SFX.ui_main_button_back);
                backToParentMenu(Overworld);
            }

            base.Update();
        }

        public override void Render() {
            if (alpha > 0f) {
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);
            }
            base.Render();
        }

        /// <summary>
        /// Navigates from the current screen to a OuiGenericMenu <b>in the overworld</b>. Does NOT work from the pause menu.
        /// </summary>
        /// <typeparam name="T">The OuiGenericMenu to navigate to</typeparam>
        /// <param name="backToParentMenu">An action to come back to the current menu (generally <code>overworld.Goto&lt;ParentMenuType&gt;()</code></param>
        /// <param name="parameters">Optional parameters the menu will get in its "options" attribute</param>
        public static void Goto<T>(Action<Overworld> backToParentMenu, params object[] parameters) where T : OuiGenericMenu {
            // get the instance for the menu we want to go to (all Oui's are singletons)
            Overworld overworld = OuiModOptions.Instance.Overworld;
            OuiGenericMenu menuInstance = overworld.GetUI<T>();

            // set up the menu instance
            menuInstance.backToParentMenu = backToParentMenu;
            menuInstance.parameters = parameters;

            // then navigate to it
            overworld.Goto<T>();
        }
    }
}
