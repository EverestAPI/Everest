﻿using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod {
    [MonoModLinkFrom("Celeste.Mod.ModuleSettingsButtonConfigUIV2")] // Holdover from 1.3.1.2 -> 1.4.0.0 input change
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
            Reload(2);
        }

        public override void Reload(int index = -1) {
            if (Module == null)
                return;

            Clear();
            Add(new Header(Dialog.Clean("BTN_CONFIG_TITLE")));
            Add(new InputMappingInfo(true));

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

                    Bindings.Add(new ButtonBindingEntry(binding, defaults));
                    
                    string subheader = prop.GetCustomAttribute<SettingSubHeaderAttribute>()?.SubHeader;
                    if (subheader != null)
                        Add(new SubHeader(subheader.DialogCleanOrNull() ?? subheader));
                    
                    AddMapForceLabel(name, binding.Binding);
                }
            }

            Add(new patch_TextMenu.patch_SubHeader(""));
            Add(new Button(Dialog.Clean("KEY_CONFIG_RESET")) {
                IncludeWidthInMeasurement = false,
                AlwaysCenter = true,
                OnPressed = () => ResetPressed()
            });

            if (index >= 0)
                Selection = index;
        }

        public override void Reset() {
            foreach (ButtonBindingEntry entry in Bindings) {
                entry.Binding.Binding.Controller.Clear();
                if (entry.Defaults != null && entry.Defaults.Button != 0)
                    entry.Binding.Binding.Controller.Add(entry.Defaults.Button);
            }
            Input.Initialize();
            Reload(Selection);
        }

        protected class ButtonBindingEntry {

            public ButtonBinding Binding;
            public DefaultButtonBindingAttribute Defaults;

            public ButtonBindingEntry(ButtonBinding binding, DefaultButtonBindingAttribute defaults) {
                Binding = binding;
                Defaults = defaults;
            }

        }

    }
}
