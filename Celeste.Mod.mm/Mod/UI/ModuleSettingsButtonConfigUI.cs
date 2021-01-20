using Microsoft.Xna.Framework.Input;
using Monocle;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod {
    public class ModuleSettingsButtonConfigUI : patch_ButtonConfigUI {

        public EverestModule Module;

        protected List<ButtonBindingEntry> Bindings = new List<ButtonBindingEntry>();

        public ModuleSettingsButtonConfigUI(EverestModule module) {
            All.Add(Buttons.Back);
            All.Add(Buttons.BigButton);
            All.Add(Buttons.RightStick);
            All.Add(Buttons.LeftStick);

            Module = module;
            // Base already reloads too early before the module has been set.
            Reload();
        }

        protected override string GetLabel(int mapping) {
            return Bindings[mapping].Name;
        }

        protected override List<Buttons> GetRemapList(int remapping, Buttons newBtn) {
            return Bindings[remapping].Binding.Buttons;
        }

        protected override void ForceRemapAll() {
        }

        public override void Reload(int index = -1) {
            if (Module == null)
                return;

            Clear();
            Add(new Header(Dialog.Clean("BTN_CONFIG_TITLE")));
            Add(new Info());

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
                    AddButtonConfigLine(mapping, defaults != null && defaults.Button != 0 && defaults.ForceDefaultButton ? ForceDefaultButton(defaults.Button, binding.Buttons) : binding.Buttons);
                }
            }

            Add(new SubHeader(""));
            Add(new Button(Dialog.Clean("KEY_CONFIG_RESET")) {
                IncludeWidthInMeasurement = false,
                AlwaysCenter = true,
                OnPressed = () => {
                    Settings.Instance.SetDefaultButtonControls(reset: true);
                    Input.Initialize();
                    Reload(Selection);
                }
            });

            if (index >= 0)
                Selection = index;
        }

        protected class ButtonBindingEntry {

            public string Name;
            public ButtonBinding Binding;
            public DefaultButtonBindingAttribute Defaults;

            public ButtonBindingEntry(string name, ButtonBinding binding, DefaultButtonBindingAttribute defaults) {
                Name = name;
                Binding = binding;
                Defaults = defaults;
            }

        }

    }
}
