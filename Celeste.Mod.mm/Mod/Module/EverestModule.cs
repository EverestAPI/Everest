using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Helpers;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    /// <summary>
    /// Your Everest main mod class inherits from this class.
    /// </summary>
    public abstract class EverestModule {

        /// <summary>
        /// Used by Everest itself to store any module metadata.
        /// 
        /// The metadata is usually parsed from meta.yaml in the archive.
        /// 
        /// You can override this property to provide dynamic metadata at runtime.
        /// Note that this doesn't affect mod loading.
        /// </summary>
        public virtual EverestModuleMetadata Metadata { get; set; }

        /// <summary>
        /// The type used for the settings object. Used for serialization, among other things.
        /// </summary>
        public abstract Type SettingsType { get; }
        /// <summary>
        /// Any settings stored across runs. Everest loads this before Load gets invoked.
        /// Define your custom property returning _Settings typecasted as your custom settings type.
        /// </summary>
        public virtual EverestModuleSettings _Settings { get; set; }

        /// <summary>
        /// Load the mod settings. Loads the settings from {Everest.PathSettings}/{Metadata.Name}.yaml by default.
        /// </summary>
        public virtual void LoadSettings() {
            if (SettingsType == null)
                return;

            _Settings = (EverestModuleSettings) SettingsType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);

            string extension = ".yaml";
            if (_Settings is EverestModuleBinarySettings)
                extension = ".bin";

            string path = Path.Combine(Everest.PathSettings, Metadata.Name + extension);
            if (!File.Exists(path))
                return;

            try {
                using (Stream stream = File.OpenRead(path)) {
                    if (_Settings is EverestModuleBinarySettings) {
                        // .bin
                        using (BinaryReader reader = new BinaryReader(stream))
                            ((EverestModuleBinarySettings) _Settings).Read(reader);
                    } else {
                        // .yaml
                        using (StreamReader reader = new StreamReader(path)) {
                            _Settings = (EverestModuleSettings) YamlHelper.Deserializer.Deserialize(reader, SettingsType);
                        }
                    }
                }
            } catch {
            }
        }

        /// <summary>
        /// Save the mod settings. Saves the settings to {Everest.PathSettings}/{Metadata.Name}.yaml by default.
        /// </summary>
        public virtual void SaveSettings() {
            if (SettingsType == null || _Settings == null)
                return;

            string extension = ".yaml";
            if (_Settings is EverestModuleBinarySettings)
                extension = ".bin";

            string path = Path.Combine(Everest.PathSettings, Metadata.Name + extension);
            if (File.Exists(path))
                File.Delete(path);

            using (Stream stream = File.OpenWrite(path)) {
                if (_Settings is EverestModuleBinarySettings) {
                    // .bin
                    using (BinaryWriter writer = new BinaryWriter(stream))
                        ((EverestModuleBinarySettings) _Settings).Write(writer);
                } else {
                    // .yaml
                    using (StreamWriter writer = new StreamWriter(stream))
                        YamlHelper.Serializer.Serialize(writer, _Settings, SettingsType);
                }
            }

        }

        /// <summary>
        /// The type used for the save data object. Used for serialization, among other things.
        /// </summary>
        public virtual Type SaveDataType => null;
        /// <summary>
        /// Any save data stored across runs.
        /// Define your custom property returning _SaveData typecasted as your custom save data type.
        /// </summary>
        public virtual EverestModuleSaveData _SaveData { get; set; }

        /// <summary>
        /// Load the mod save data. Loads the save data from {Everest.PathSettings}/Save{index}/{Metadata.Name}.yaml by default.
        /// </summary>
        public virtual void LoadSaveData(int index) {
            if (SaveDataType == null)
                return;

            _SaveData = (EverestModuleSaveData) SaveDataType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);
            _SaveData.Index = index;

            string extension = ".yaml";
            if (_SaveData is EverestModuleBinarySaveData)
                extension = ".bin";

            string path = Path.Combine(Everest.PathSettings, "Save" + index, Metadata.Name + extension);
            if (!File.Exists(path))
                return;

            try {
                using (Stream stream = File.OpenRead(path)) {
                    if (_SaveData is EverestModuleBinarySaveData) {
                        // .bin
                        using (BinaryReader reader = new BinaryReader(stream))
                            ((EverestModuleBinarySaveData) _SaveData).Read(reader);
                    } else {
                        // .yaml
                        using (StreamReader reader = new StreamReader(path)) {
                            _SaveData = (EverestModuleSaveData) YamlHelper.Deserializer.Deserialize(reader, SaveDataType);
                            _SaveData.Index = index;
                        }
                    }
                }
            } catch {
            }
        }

        /// <summary>
        /// Save the mod save data. Saves the save data to {Everest.PathSettings}/Save{index}/{Metadata.Name}.yaml by default.
        /// </summary>
        public virtual void SaveSaveData(int index) {
            if (SaveDataType == null)
                return;
            string path = Path.Combine(Everest.PathSettings, "Save" + index, Metadata.Name + ".yaml");
            if (File.Exists(path))
                File.Delete(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (Stream stream = File.OpenWrite(path))
            using (StreamWriter writer = new StreamWriter(stream))
                YamlHelper.Serializer.Serialize(writer, _SaveData, SaveDataType);
        }

        /// <summary>
        /// Perform any initializing actions after all mods have been loaded.
        /// Do not depend on any specific order in which the mods get initialized.
        /// </summary>
        public abstract void Load();

        /// <summary>
        /// Perform any initializing actions after Celeste.Initialize has been called.
        /// Do not depend on any specific order in which the mods get initialized.
        /// </summary>
        public virtual void Initialize() {
        }

        [Obsolete("Override LoadContent(bool firstLoad) instead.")]
        /// <summary>
        /// Perform any content loading actions after Celeste.LoadContent has been called.
        /// </summary>
        public virtual void LoadContent() {
        }

        /// <summary>
        /// Perform any content loading actions after Celeste.LoadContent has been called.
        /// </summary>
        /// <param name="firstLoad">Is this the first load?</param>
        public virtual void LoadContent(bool firstLoad) {
#pragma warning disable CS0618 // Type or member is obsolete
            LoadContent();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Unload any unmanaged resources allocated by the mod (f.e. textures) and
        /// undo any changes performed by the mod.
        /// </summary>
        public abstract void Unload();

        /// <summary>
        /// Parse the current command-line argument and any follow-ups.
        /// </summary>
        /// <param name="arg">The current command line argument.</param>
        /// <param name="args">Any further arguments the mod may want to dequeue and parse.</param>
        /// <returns>True if the argument "belongs" to the mod, false otherwise.</returns>
        public virtual bool ParseArg(string arg, Queue<string> args) {
            return false;
        }

        private Type _PrevSettingsType;
        private PropertyInfo[] _PrevSettingsProps;
        /// <summary>
        /// Create the mod menu subsection including the section header in the given menu.
        /// The default implementation uses reflection to attempt creating a menu.
        /// </summary>
        /// <param name="menu">Menu to add the section to.</param>
        /// <param name="inGame">Whether we're in-game (paused) or in the main menu.</param>
        /// <param name="snapshot">The Level.PauseSnapshot</param>
        public virtual void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            Type type = SettingsType;
            EverestModuleSettings settings = _Settings;
            if (type == null || settings == null)
                return;

            // The default name prefix.
            string typeName = type.Name.ToLowerInvariant();
            if (typeName.EndsWith("settings"))
                typeName = typeName.Substring(0, typeName.Length - 8);
            string nameDefaultPrefix = $"modoptions_{typeName}_";

            // Any attributes we may want to get and read from later.
            SettingInGameAttribute attribInGame;
            SettingRangeAttribute attribRange;

            // If the settings type has got the InGame attrib, only show it in the matching situation.
            if ((attribInGame = type.GetCustomAttribute<SettingInGameAttribute>()) != null &&
                attribInGame.InGame != inGame)
                return;

            // The settings subheader.
            string name; // We lazily reuse this field for the props later on.
            name = type.GetCustomAttribute<SettingNameAttribute>()?.Name ?? $"{nameDefaultPrefix}title";
            name = name.DialogCleanOrNull() ?? Metadata.Name.SpacedPascalCase();

            menu.Add(new TextMenu.SubHeader(name + " | v." + Metadata.VersionString));

            PropertyInfo[] props;
            if (type == _PrevSettingsType) {
                props = _PrevSettingsProps;
            } else {
                _PrevSettingsProps = props = type.GetProperties();
                _PrevSettingsType = type;
            }

            foreach (PropertyInfo prop in props) {
                MethodInfo creator = type.GetMethod(
                    $"Create{prop.Name}Entry",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(TextMenu), typeof(bool) },
                    new ParameterModifier[0]
                );

                if (creator != null) {
                    creator.GetDelegate()(settings, menu, inGame);
                    continue;
                }

                if ((attribInGame = prop.GetCustomAttribute<SettingInGameAttribute>()) != null &&
                    attribInGame.InGame != inGame)
                    continue;

                if (prop.GetCustomAttribute<SettingIgnoreAttribute>() != null)
                    continue;

                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                name = prop.GetCustomAttribute<SettingNameAttribute>()?.Name ?? $"{nameDefaultPrefix}{prop.Name.ToLowerInvariant()}";
                name = name.DialogCleanOrNull() ?? prop.Name.SpacedPascalCase();

                bool needsRelaunch = prop.GetCustomAttribute<SettingNeedsRelaunchAttribute>() != null;

                TextMenu.Item item = null;
                Type propType = prop.PropertyType;
                object value = prop.GetValue(settings);

                // Create the matching item based off of the type and attributes.

                if (propType == typeof(bool)) {
                    item =
                        new TextMenu.OnOff(name, (bool) value)
                        .Change(v => prop.SetValue(settings, v))
                    ;

                } else if (
                    propType == typeof(int) &&
                    (attribRange = prop.GetCustomAttribute<SettingRangeAttribute>()) != null
                ) {
                    item =
                        new TextMenu.Slider(name, i => i.ToString(), attribRange.Min, attribRange.Max, (int) value)
                        .Change(v => prop.SetValue(settings, v))
                    ;

                } else if (propType.IsEnum) {
                    Array enumValues = Enum.GetValues(propType);
                    Array.Sort((int[]) enumValues);
                    string enumNamePrefix = $"{nameDefaultPrefix}{prop.Name.ToLowerInvariant()}_";
                    item =
                        new TextMenu.Slider(name, (i) => {
                            string enumName = enumValues.GetValue(i).ToString();
                            string fullName = $"{enumNamePrefix}{enumName.ToLowerInvariant()}";
                            return fullName.DialogCleanOrNull() ?? enumName;
                        }, 0, enumValues.Length - 1, (int) value)
                        .Change(v => prop.SetValue(settings, v))
                    ;

                } else if (!inGame && propType == typeof(string)) {
                    item =
                        new TextMenu.Button(name + ": " + value)
                        .Pressed(() => {
                            Audio.Play("event:/ui/main/savefile_rename_start");
                            menu.SceneAs<Overworld>().Goto<OuiModOptionString>().Init<OuiModOptions>(
                                (string) value,
                                v => prop.SetValue(settings, v)
                            );
                        })
                    ;
                }

                if (item == null)
                    continue;

                menu.Add(item.NeedsRelaunch(needsRelaunch));
            }

        }

    }
}
