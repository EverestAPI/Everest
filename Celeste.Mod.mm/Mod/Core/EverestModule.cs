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
        /// Any settings stored across runs. Everest loads this before Load gets invoked.
        /// </summary>
        public virtual EverestModuleSettings Settings { get; set; }
        public virtual T GetSettings<T>() where T : EverestModuleSettings
            => (T) Settings;

        /// <summary>
        /// Perform any initializing actions after all modd have been loaded.
        /// Do not depend on any specific order in which the mods get initialized.
        /// </summary>
        public abstract void Load();

        /// <summary>
        /// Unload any unmanaged resources allocated by the mod (f.e. textures) and
        /// undo any changes performed by the mod.
        /// </summary>
        public abstract void Unload();

        /// <summary>
        /// Create the mod menu subsection including the section header in the given menu.
        /// </summary>
        /// <param name="menu">Menu to add the section to.</param>
        /// <param name="inGame">Whether we're in-game (paused) or in the main menu.</param>
        /// <param name="snapshot">The Level.PauseSnapshot</param>
        public virtual void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {

        }

    }
}
