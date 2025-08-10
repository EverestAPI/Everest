#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using FMOD.Studio;
using Celeste.Mod.Core;
using MonoMod;
using System.Diagnostics;

namespace Celeste {
    class patch_MenuOptions {

        public static extern TextMenu orig_Create(bool inGame = false, EventInstance snapshot = null);
        public static TextMenu Create(bool inGame = false, EventInstance snapshot = null) {

            // Create the original options menu
            patch_TextMenu menu = (patch_TextMenu) orig_Create(inGame, snapshot);

            // Create all of our submenu options and their descriptions
            // TODO: Make this less redundant using a loop and reflection
            TextMenu.Item distort = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_COREMODULE_PSDISTORT"), CoreModule.Settings.PhotosensitivityDistortOverride)
                .Change(value => {
                    CoreModule.Settings.PhotosensitivityDistortOverride = value;
                });
            var distortDesc = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean("MODOPTIONS_COREMODULE_PSDISTORT_DESC"), false, menu) { HeightExtra = 0f };
            distort.OnEnter += () => distortDesc.FadeVisible = true;
            distort.OnLeave += () => distortDesc.FadeVisible = false;

            TextMenu.Item glitch = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_COREMODULE_PSGLITCH"), CoreModule.Settings.PhotosensitivityGlitchOverride)
                .Change(value => {
                    CoreModule.Settings.PhotosensitivityGlitchOverride = value;
                });
            var glitchDesc = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean("MODOPTIONS_COREMODULE_PSGLITCH_DESC"), false, menu) { HeightExtra = 0f };
            glitch.OnEnter += () => glitchDesc.FadeVisible = true;
            glitch.OnLeave += () => glitchDesc.FadeVisible = false;

            TextMenu.Item lightning = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_COREMODULE_PSLIGHTNING"), CoreModule.Settings.PhotosensitivityLightningOverride)
                .Change(value => {
                    CoreModule.Settings.PhotosensitivityLightningOverride = value;
                });
            var lightningDesc = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean("MODOPTIONS_COREMODULE_PSLIGHTNING_DESC"), false, menu) { HeightExtra = 0f };
            lightning.OnEnter += () => lightningDesc.FadeVisible = true;
            lightning.OnLeave += () => lightningDesc.FadeVisible = false;

            TextMenu.Item screenFlash = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_COREMODULE_PSSCREENFLASH"), CoreModule.Settings.PhotosensitivityScreenFlashOverride)
                .Change(value => {
                    CoreModule.Settings.PhotosensitivityScreenFlashOverride = value;
                });
            var screenFlashDesc = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean("MODOPTIONS_COREMODULE_PSSCREENFLASH_DESC"), false, menu) { HeightExtra = 0f };
            screenFlash.OnEnter += () => screenFlashDesc.FadeVisible = true;
            screenFlash.OnLeave += () => screenFlashDesc.FadeVisible = false;

            TextMenu.Item textHighlight = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_COREMODULE_PSTEXTHIGHLIGHT"), CoreModule.Settings.PhotosensitivityTextHighlightOverride)
                .Change(value => {
                    CoreModule.Settings.PhotosensitivityTextHighlightOverride = value;
                });
            var textHighlightDesc = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean("MODOPTIONS_COREMODULE_PSTEXTHIGHLIGHT_DESC"), false, menu) { HeightExtra = 0f };
            textHighlight.OnEnter += () => textHighlightDesc.FadeVisible = true;
            textHighlight.OnLeave += () => textHighlightDesc.FadeVisible = false;

            // Put all the options into a big submenu
            TextMenuExt.SubMenu submenu = new TextMenuExt.SubMenu(Dialog.Clean("MODOPTIONS_COREMODULE_PSOPTIONS"), false)
                .Add(distort)
                .Add(distortDesc)
                .Add(glitch)
                .Add(glitchDesc)
                .Add(lightning)
                .Add(lightningDesc)
                .Add(screenFlash)
                .Add(screenFlashDesc)
                .Add(textHighlight)
                .Add(textHighlightDesc);
            
            // Create a master switch that toggles the submenu to replace the existing photosensitive mode option
            TextMenu.Item masterSwitch = new TextMenu.OnOff(Dialog.Clean("OPTIONS_DISABLE_FLASH"), Settings.Instance.DisableFlashes)
                .Change(value => {
                    Settings.Instance.DisableFlashes = value;
                    submenu.Disabled = !value;
                });

            // Remove the existing photosensitive menu and replace it with our master switch and submenu
            menu.ReplaceByLabel<TextMenu.OnOff>("OPTIONS_DISABLE_FLASH", masterSwitch, submenu);

            // Disable the submenu if necessary
            submenu.Disabled = !Settings.Instance.DisableFlashes;

            // Create our new Window Mode option
            // TODO: Create Dialog for menu item
            TextMenu.Item windowMode = new TextMenuExt.EnumSlider<GameWindowMode>(Dialog.Clean("MODOPTIONS_CODEMODULE_WMSLIDER"), ((patch_Settings) Settings.Instance).WindowMode,
                MenuOptionsExt.ToDialog)
                .Change(SetWindowMode);

            // Replace Fullscreen with Window Mode Options
            menu.ReplaceByLabel<TextMenu.OnOff>("options_fullscreen", windowMode);

            // Send back the menu
            return menu;
        }

        private static patch_TextMenu.patch_Item window;

        private static patch_TextMenu.patch_Item viewport;

        private static void SetWindowMode(GameWindowMode value) {
            ((patch_Settings) Settings.Instance).WindowMode = value;
            Settings.Instance.Fullscreen = value == GameWindowMode.Fullscreen;
            ((patch_Settings) Settings.Instance).ApplyScreen();
            if (window != null) {
                window.Visible = value == GameWindowMode.Windowed;
            }
            if (viewport != null) {
                viewport.Visible = value != GameWindowMode.Windowed;
            }
        }

        //This Method would be unnecessary with the followig changes, I don't know if this is correctly removed, though
        [MonoModRemove]
        private static extern void SetFullscreen(bool on);
    }

    static class MenuOptionsExt {
        public static string ToDialog(this GameWindowMode mode) {
            return mode switch {
                GameWindowMode.Fullscreen => "options_fullscreen",
                GameWindowMode.Borderless => "MODOPTIONS_CODEMODULE_WMBORDERLESS",
                GameWindowMode.Windowed => "MODOPTIONS_CODEMODULE_WMWINDOWED",
                _ => throw new UnreachableException("Failed to get Dialog string somehow.")
            };
        }
    }
}
