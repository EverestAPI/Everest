using FMOD.Studio;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    public abstract class EverestModuleSettings {

        // If we ever need to add any methods in the future...

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class SettingNameAttribute : Attribute {
        public string Name;
        public SettingNameAttribute(string name) {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class SettingInGameAttribute : Attribute {
        public bool InGame;
        public SettingInGameAttribute(bool inGame) {
            InGame = inGame;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SettingRangeAttribute : Attribute {
        public int Min;
        public int Max;
        public SettingRangeAttribute(int min, int max) {
            Min = min;
            Max = max;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SettingNeedsRelaunchAttribute : Attribute {
        public SettingNeedsRelaunchAttribute() {
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SettingIgnoreAttribute : Attribute {
        public SettingIgnoreAttribute() {
        }
    }
}
