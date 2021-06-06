using YamlDotNet.Serialization;

namespace Celeste.Mod {
    /// <summary>
    /// Per-save-slot mod data.
    /// Everest loads / saves this for you as .yaml by default.
    /// </summary>
    public abstract class EverestModuleSaveData {

        /// <summary>
        /// The save data index. Assigned by Everest itself when loading it.
        /// </summary>
        [YamlIgnore]
        public int Index { get; set; }

    }
}
