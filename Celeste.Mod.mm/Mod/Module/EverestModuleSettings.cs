using System;

namespace Celeste.Mod {
    /// <summary>
    /// Global mod settings, which will show up in the mod options menu.
    /// Everest loads / saves this for you as .yaml by default.
    /// </summary>
    public abstract class EverestModuleSettings {

        // If we ever need to add any methods in the future...

    }

    /// <summary>
    /// The dialog key / name for the settings option.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class SettingNameAttribute : Attribute {
        public string Name;
        public SettingNameAttribute(string name) {
            Name = name;
        }
    }

    /// <summary>
    /// Whether the option should be shown in-game or in the main menu only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class SettingInGameAttribute : Attribute {
        public bool InGame;
        public SettingInGameAttribute(bool inGame) {
            InGame = inGame;
        }
    }

    /// <summary>
    /// The integer option range.
    /// If largeRange is set to true, a slider optimized for large integer ranges (going through values at an increasing speed) will be used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingRangeAttribute : Attribute {
        public int Min;
        public int Max;
        public bool LargeRange;
        public SettingRangeAttribute(int min, int max) {
            Min = min;
            Max = max;
            LargeRange = false;
        }
        public SettingRangeAttribute(int min, int max, bool largeRange) : this(min, max) {
            LargeRange = largeRange;
        }
    }

    /// <summary>
    /// Allows to set the maximum length of string settings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingMaxLengthAttribute : Attribute {
        public int Max;
        public SettingMaxLengthAttribute(int max) {
            Max = max;
        }
    }

    /// <summary>
    /// Shows a button that will open a number input screen. Supported on int and float properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingNumberInputAttribute : Attribute {
        public int MaxLength;
        public bool AllowNegatives;
        public SettingNumberInputAttribute(bool allowNegatives = true, int maxLength = 6) {
            AllowNegatives = allowNegatives;
            MaxLength = maxLength;
        }
    }

    /// <summary>
    /// Any options with this attribute will notify the user that a restart is required to apply the changes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingNeedsRelaunchAttribute : Attribute {
        public SettingNeedsRelaunchAttribute() {
        }
    }

    /// <summary>
    /// Add a description shown when the setting is selected
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingSubTextAttribute : Attribute {
        public string Description;
        public SettingSubTextAttribute(string description) {
            Description = description;
        }
    }

    /// <summary>
    /// Ignore the setting in the default mod options menu handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingIgnoreAttribute : Attribute {
        public SettingIgnoreAttribute() {
        }
    }
}
