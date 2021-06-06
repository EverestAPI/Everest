using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod {
    // This MUST keep its old name. If V1 support gets dropped, rename V2 to this and use MonoModLinkFrom V2 -> this.
    [Obsolete]
    public class ModuleSettingsKeyboardConfigUI : patch_KeyboardConfigUI_InputV1 {

        public EverestModule Module;

        protected List<ButtonBindingEntry> Bindings = new List<ButtonBindingEntry>();
        
        public ModuleSettingsKeyboardConfigUI(EverestModule module) {
            Module = module;
            // Base already reloads too early before the module has been set.
            Reload();
        }

        protected override string GetLabel(int mapping) {
            return Bindings[mapping].Name;
        }

        protected override bool SupportsMultipleBindings(int mapping) {
            return true;
        }

        protected override List<Keys> GetRemapList(int remapping, Keys newKey) {
            return Bindings[remapping].Binding.Keys;
        }

        public override void Reload(int index = -1) {
            if (Module == null)
                return;

            Clear();
            Add(new Header(Dialog.Clean("KEY_CONFIG_TITLE")));
            Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_ADDITION_HINT")));

            Bindings.Clear();

            object settings = Module._Settings;

            // The default name prefix.
            string typeName = Module.SettingsType.Name.ToLowerInvariant();
            if (typeName.EndsWith("settings"))
                typeName = typeName.Substring(0, typeName.Length - 8);
            string nameDefaultPrefix = $"modoptions_{typeName}_";

            SettingInGameAttribute attribInGame;

            foreach (PropertyInfo prop in Module.SettingsType.GetProperties()) {
                if ((attribInGame = prop.GetCustomAttribute<SettingInGameAttribute>()) != null &&
                    attribInGame.InGame != (Engine.Scene is Level))
                    continue;

                if (prop.GetCustomAttribute<SettingIgnoreAttribute>() != null)
                    continue;

                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                if (typeof(ButtonBinding).IsAssignableFrom(prop.PropertyType)) {
                    if (!(prop.GetValue(settings) is ButtonBinding binding))
                        continue;

                    int mapping = Bindings.Count;

                    string name = prop.GetCustomAttribute<SettingNameAttribute>()?.Name ?? $"{nameDefaultPrefix}{prop.Name.ToLowerInvariant()}";
                    name = name.DialogCleanOrNull() ?? (prop.Name.ToLowerInvariant().StartsWith("button") ? prop.Name.Substring(6) : prop.Name).SpacedPascalCase();

                    DefaultButtonBindingAttribute defaults = prop.GetCustomAttribute<DefaultButtonBindingAttribute>();

                    Bindings.Add(new ButtonBindingEntry(name, binding, defaults));
                    AddKeyConfigLine(mapping, defaults != null && defaults.Key != 0 && defaults.ForceDefaultKey ? ForceDefaultKey(defaults.Key, binding.Keys) : binding.Keys);
                }
            }

            Add(new patch_TextMenu.patch_SubHeader(""));
            Button reset = new Button(Dialog.Clean("KEY_CONFIG_RESET")) {
                IncludeWidthInMeasurement = false,
                AlwaysCenter = true,
                OnPressed = () => {
                    foreach (ButtonBindingEntry entry in Bindings) {
                        entry.Binding.Keys.Clear();
                        if (entry.Defaults != null && entry.Defaults.Key != 0)
                            entry.Binding.Keys.Add(entry.Defaults.Key);
                    }
                    Input.Initialize();
                    Reload(Selection);
                }
            };
            Add(reset);

            if (index >= 0)
                Selection = index;
        }

        protected class ButtonBindingEntry {

            public string Name;
            [Obsolete]
            public bool SupportsMultipleBindings;
            public ButtonBinding Binding;
            public DefaultButtonBindingAttribute Defaults;

            public ButtonBindingEntry(string name, ButtonBinding binding, DefaultButtonBindingAttribute defaults) {
                Name = name;
                Binding = binding;
                Defaults = defaults;
            }

            [Obsolete]
            public ButtonBindingEntry(string name, bool supportsMultipleBindings, ButtonBinding binding, DefaultButtonBindingAttribute defaults) {
                Name = name;
                SupportsMultipleBindings = true;
                Binding = binding;
                Defaults = defaults;
            }

        }

    }
}
