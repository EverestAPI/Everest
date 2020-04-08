using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Utils;
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
        public virtual Type SettingsType => null;
        /// <summary>
        /// Any settings stored across runs. Everest loads this before Load gets invoked.
        /// Define your custom property returning _Settings typecasted as your custom settings type.
        /// </summary>
        public virtual EverestModuleSettings _Settings { get; set; }

        /// <summary>
        /// Load the mod settings. Loads the settings from {UserIO.GetSavePath("Saves")}/modsettings-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual void LoadSettings() {
            if (SettingsType == null)
                return;

            _Settings = (EverestModuleSettings) SettingsType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);

            string path = patch_UserIO.GetSaveFilePath("modsettings-" + Metadata.Name);

            // Temporary fallback to help migrate settings from their old location.
            if (!File.Exists(path))
                path = Path.Combine(Everest.PathEverest, "ModSettings-OBSOLETE", Metadata.Name + ".yaml");

            if (!File.Exists(path))
                return;

            try {
                using (Stream stream = File.OpenRead(path)) {
                    if (_Settings is EverestModuleBinarySettings) {
                        using (BinaryReader reader = new BinaryReader(stream))
                            ((EverestModuleBinarySettings) _Settings).Read(reader);
                    } else {
                        using (StreamReader reader = new StreamReader(stream))
                            YamlHelper.DeserializerUsing(_Settings).Deserialize(reader, SettingsType);
                    }
                }
            } catch {
            }

            if (_Settings == null)
                _Settings = (EverestModuleSettings) SettingsType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);
        }

        /// <summary>
        /// Save the mod settings. Saves the settings to {UserIO.GetSavePath("Saves")}/modsettings-{Metadata.Name}.yaml by default.
        /// </summary>
        public virtual void SaveSettings() {
            if (SettingsType == null || _Settings == null)
                return;

            string path = patch_UserIO.GetSaveFilePath("modsettings-" + Metadata.Name);
            if (File.Exists(path))
                File.Delete(path);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            try {
                using (Stream stream = File.OpenWrite(path)) {
                    if (_Settings is EverestModuleBinarySettings) {
                        using (BinaryWriter writer = new BinaryWriter(stream))
                            ((EverestModuleBinarySettings) _Settings).Write(writer);
                    } else {
                        using (StreamWriter writer = new StreamWriter(stream))
                            YamlHelper.Serializer.Serialize(writer, _Settings, SettingsType);
                    }
                }
            } catch {
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
        /// Load the mod save data. Loads the save data from {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsave-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual void LoadSaveData(int index) {
            if (SaveDataType == null)
                return;

            _SaveData = (EverestModuleSaveData) SaveDataType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);
            _SaveData.Index = index;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsave-" + Metadata.Name);
            if (!File.Exists(path))
                return;

            try {
                using (Stream stream = File.OpenRead(path)) {
                    if (_SaveData is EverestModuleBinarySaveData) {
                        using (BinaryReader reader = new BinaryReader(stream))
                            ((EverestModuleBinarySaveData) _SaveData).Read(reader);
                    } else {
                        using (StreamReader reader = new StreamReader(stream))
                            YamlHelper.DeserializerUsing(_SaveData).Deserialize(reader, SaveDataType);
                    }
                }
                _SaveData.Index = index;
            } catch {
            }

        }

        /// <summary>
        /// Save the mod save data. Saves the save data to {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsave-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual void SaveSaveData(int index) {
            if (SaveDataType == null)
                return;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsave-" + Metadata.Name);
            if (File.Exists(path))
                File.Delete(path);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            try {
                using (Stream stream = File.OpenWrite(path)) {
                    if (_SaveData is EverestModuleBinarySaveData) {
                        using (BinaryWriter writer = new BinaryWriter(stream))
                            ((EverestModuleBinarySaveData) _SaveData).Write(writer);
                    } else {
                        using (StreamWriter writer = new StreamWriter(stream))
                            YamlHelper.Serializer.Serialize(writer, _SaveData, SaveDataType);
                    }
                }
            } catch {
            }
        }

        /// <summary>
        /// Delete the mod save data. Deletes the save data at {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsave-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual void DeleteSaveData(int index) {
            if (SaveDataType == null)
                return;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsave-" + Metadata.Name);
            if (!File.Exists(path))
                return;

            File.Delete(path);
        }

        /// <summary>
        /// The type used for the session object. Used for serialization, among other things.
        /// </summary>
        public virtual Type SessionType => null;
        /// <summary>
        /// Any save data stored for the current session.
        /// Define your custom property returning _Session typecasted as your custom session type.
        /// </summary>
        public virtual EverestModuleSession _Session { get; set; }

        /// <summary>
        /// Load the mod session. Loads the session from {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsession-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual void LoadSession(int index, bool forceNew) {
            if (SessionType == null)
                return;

            _Session = (EverestModuleSession) SessionType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);
            _Session.Index = index;

            if (forceNew)
                return;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsession-" + Metadata.Name);
            if (!File.Exists(path))
                return;

            try {
                using (Stream stream = File.OpenRead(path)) {
                    if (_Session is EverestModuleBinarySession) {
                        using (BinaryReader reader = new BinaryReader(stream))
                            ((EverestModuleBinarySession) _Session).Read(reader);
                    } else {
                        using (StreamReader reader = new StreamReader(stream))
                            YamlHelper.DeserializerUsing(_Session).Deserialize(reader, SessionType);
                    }
                }
                _Session.Index = index;
            } catch {
            }
        }

        /// <summary>
        /// Save the mod session. Saves the session to {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsession-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual void SaveSession(int index) {
            if (SessionType == null)
                return;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsession-" + Metadata.Name);
            if (File.Exists(path))
                File.Delete(path);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            try {
                using (Stream stream = File.OpenWrite(path)) {
                    if (_Session is EverestModuleBinarySession) {
                        using (BinaryWriter writer = new BinaryWriter(stream))
                            ((EverestModuleBinarySession) _Session).Write(writer);
                    } else {
                        using (StreamWriter writer = new StreamWriter(stream))
                            YamlHelper.Serializer.Serialize(writer, _Session, SessionType);
                    }
                }
            } catch {
            }
        }

        /// <summary>
        /// Delete the mod session. Deletes the session at {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsession-{Metadata.Name}.celeste by default.
        /// </summary>
        /// <param name="index"></param>
        public virtual void DeleteSession(int index) {
            if (SessionType == null)
                return;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsession-" + Metadata.Name);
            if (!File.Exists(path))
                return;

            File.Delete(path);
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

        /// <summary>
        /// Perform any content loading actions after Celeste.LoadContent has been called.
        /// </summary>
        [Obsolete("Override LoadContent(bool firstLoad) instead.")]
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

        protected virtual void CreateModMenuSectionHeader(TextMenu menu, bool inGame, EventInstance snapshot) {
            Type type = SettingsType;
            EverestModuleSettings settings = _Settings;
            if (type == null || settings == null)
                return;

            string typeName = type.Name.ToLowerInvariant();
            if (typeName.EndsWith("settings"))
                typeName = typeName.Substring(0, typeName.Length - 8);
            string nameDefaultPrefix = $"modoptions_{typeName}_";

            string name; // We lazily reuse this field for the props later on.
            name = type.GetCustomAttribute<SettingNameAttribute>()?.Name ?? $"{nameDefaultPrefix}title";
            name = name.DialogCleanOrNull() ?? Metadata.Name.SpacedPascalCase();

            menu.Add(new TextMenu.SubHeader(name + " | v." + Metadata.VersionString));
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

            bool headerCreated = false;
            if (GetType().GetMethod("CreateModMenuSection").DeclaringType != typeof(EverestModule)) {
                CreateModMenuSectionHeader(menu, inGame, snapshot);
                headerCreated = true;
            }

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
                    if (!headerCreated) {
                        CreateModMenuSectionHeader(menu, inGame, snapshot);
                        headerCreated = true;
                    }

                    creator.GetFastDelegate()(settings, menu, inGame);
                    continue;
                }

                if ((attribInGame = prop.GetCustomAttribute<SettingInGameAttribute>()) != null &&
                    attribInGame.InGame != inGame)
                    continue;

                if (prop.GetCustomAttribute<SettingIgnoreAttribute>() != null)
                    continue;

                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                string name = prop.GetCustomAttribute<SettingNameAttribute>()?.Name ?? $"{nameDefaultPrefix}{prop.Name.ToLowerInvariant()}";
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

                    if (attribRange.LargeRange) {
                        item =
                            new TextMenuExt.IntSlider(name, attribRange.Min, attribRange.Max, (int) value)
                            .Change(v => prop.SetValue(settings, v))
                        ;
                    } else {
                        item =
                            new TextMenu.Slider(name, i => i.ToString(), attribRange.Min, attribRange.Max, (int) value)
                            .Change(v => prop.SetValue(settings, v))
                        ;
                    }

                } else if (propType.IsEnum) {
                    Array enumValues = Enum.GetValues(propType);
                    Array.Sort((int[]) enumValues);
                    string enumNamePrefix = $"{nameDefaultPrefix}{prop.Name.ToLowerInvariant()}_";
                    item =
                        new TextMenu.Slider(name, (i) => {
                            string enumName = enumValues.GetValue(i).ToString();
                            return
                                $"{enumNamePrefix}{enumName.ToLowerInvariant()}".DialogCleanOrNull() ??
                                $"modoptions_{propType.Name.ToLowerInvariant()}_{enumName.ToLowerInvariant()}".DialogCleanOrNull() ??
                                enumName;
                        }, 0, enumValues.Length - 1, (int) value)
                        .Change(v => prop.SetValue(settings, v))
                    ;

                } else if (!inGame && propType == typeof(string)) {
                    int maxValueLength = prop.GetCustomAttribute<SettingMaxLengthAttribute>()?.Max ?? 12;

                    item =
                        new TextMenu.Button(name + ": " + value)
                        .Pressed(() => {
                            Audio.Play(SFX.ui_main_savefile_rename_start);
                            menu.SceneAs<Overworld>().Goto<OuiModOptionString>().Init<OuiModOptions>(
                                (string) value,
                                v => prop.SetValue(settings, v),
                                maxValueLength
                            );
                        })
                    ;
                }

                if (item == null)
                    continue;

                if (!headerCreated) {
                    CreateModMenuSectionHeader(menu, inGame, snapshot);
                    headerCreated = true;
                }

                menu.Add(item);

                if (needsRelaunch)
                    item = item.NeedsRelaunch(menu);
            }

        }

        /// <summary>
        /// Create and add any map data processors to the given context, if any are needed.
        /// </summary>
        /// <param name="context">The context to add the processors to.</param>
        public virtual void PrepareMapDataProcessors(MapDataFixup context) {
        }

    }
}
