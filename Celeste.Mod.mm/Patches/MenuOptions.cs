#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using FMOD.Studio;
using Celeste.Mod.Core;
using System.Reflection;

namespace Celeste {
    class patch_MenuOptions {

        private static string[] photosensitivityOptions = new string[] {
            "Distort",
            "Glitch",
            "Lightning",
            "ScreenFlash",
            "TextHighlight"
        };

        public static extern TextMenu orig_Create(bool inGame = false, EventInstance snapshot = null);
        public static TextMenu Create(bool inGame = false, EventInstance snapshot = null) {
            // Create the original options menu
            patch_TextMenu menu = (patch_TextMenu) orig_Create(inGame, snapshot);

            // Get the index of the photosensitive mode option
            int photosensitiveIndex = menu.Items.FindIndex(
                item => item is TextMenu.OnOff onOff
                     && onOff.Label == Dialog.Clean("OPTIONS_DISABLE_FLASH")
            );

            // Put all the options into a big submenu
            TextMenuExt.SubMenu submenu = new TextMenuExt.SubMenu(Dialog.Clean("MODOPTIONS_COREMODULE_PSOPTIONS"), false);
            foreach (string name in photosensitivityOptions)
                makePhotosentivityItem(menu, submenu, name);

            // Create a master switch that toggles the submenu to replace the existing photosensitive mode option
            TextMenu.Item masterSwitch = new TextMenu.OnOff(Dialog.Clean("OPTIONS_DISABLE_FLASH"), Settings.Instance.DisableFlashes)
                .Change(value => {
                    Settings.Instance.DisableFlashes = value;
                    submenu.Disabled = !value;
                });

            // Remove the existing photosensitive menu and replace it with our master switch
            menu.Remove(menu.Items[photosensitiveIndex]);
            menu.Insert(photosensitiveIndex, masterSwitch);

            // Insert the submenu at the correct point
            menu.Insert(photosensitiveIndex + 1, submenu);

            // Disable the submenu if necessary
            submenu.Disabled = !Settings.Instance.DisableFlashes;

            // Also create a colorblindness accessibility switch
            TextMenu.Item colorblindness = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_COREMODULE_COLORBLINDNESS"), CoreModule.Settings.ColourblindnessAccessibility)
                .Change(value => {
                    CoreModule.Settings.ColourblindnessAccessibility = value;
                });
            var colorblindnessDesc = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean("MODOPTIONS_COREMODULE_COLORBLINDNESS_DESC"), false, menu) { HeightExtra = 0f };
            colorblindness.OnEnter += () => colorblindnessDesc.FadeVisible = true;
            colorblindness.OnLeave += () => colorblindnessDesc.FadeVisible = false;

            menu.Insert(photosensitiveIndex + 2, colorblindness);
            menu.Insert(photosensitiveIndex + 3, colorblindnessDesc);

            // Send back the menu
            return menu;
        }

        private static void makePhotosentivityItem(patch_TextMenu menu, TextMenuExt.SubMenu submenu, string name) {
            string key = $"MODOPTIONS_COREMODULE_PS{name.ToUpper()}";
            string setting = $"Photosensitivity{name}Override";
            PropertyInfo property = typeof(CoreModuleSettings).GetProperty(setting);

            TextMenu.Item item = new TextMenu.OnOff(Dialog.Clean(key), (bool)property.GetValue(CoreModule.Settings))
                .Change(value => {
                    property.SetValue(CoreModule.Settings, value);
                });
            var desc = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean(key + "_DESC"), false, menu) { HeightExtra = 0f };
            item.OnEnter += () => desc.FadeVisible = true;
            item.OnLeave += () => desc.FadeVisible = false;

            submenu.Add(item).Add(desc);
        }
    }
}
