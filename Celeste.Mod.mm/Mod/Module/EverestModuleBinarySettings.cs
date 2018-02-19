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
    /// <summary>
    /// Global mod settings, binary formatted, which will show up in the mod options menu.
    /// Everest loads / saves this for you as .bin by default.
    /// </summary>
    public abstract class EverestModuleBinarySettings : EverestModuleSettings {

        /// <summary>
        /// Read the settings from the given BinaryReader to the current object.
        /// </summary>
        public abstract void Read(BinaryReader reader);
        /// <summary>
        /// Write the settings from the current object to the given BinaryWriter.
        /// </summary>
        public abstract void Write(BinaryWriter writer);

    }
}
