using System.IO;

namespace Celeste.Mod {
    /// <summary>
    /// Per-session mod data, binary formatted.
    /// Everest loads / saves this for you as .bin by default.
    /// </summary>
    public abstract class EverestModuleBinarySession : EverestModuleSession {

        /// <summary>
        /// Read the session from the given BinaryReader to the current object.
        /// </summary>
        public abstract void Read(BinaryReader reader);
        /// <summary>
        /// Write the session from the current object to the given BinaryWriter.
        /// </summary>
        public abstract void Write(BinaryWriter writer);

    }
}
