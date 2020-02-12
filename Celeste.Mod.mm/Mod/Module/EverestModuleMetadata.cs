using Celeste.Mod.Core;
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
    /// Any module metadata, usually mirroring the data in your metadata.yaml
    /// </summary>
    public class EverestModuleMetadata {

        /// <summary>
        /// The collection of mod metadatas this mod metadata is part of. Set at runtime.
        /// </summary>
        public virtual EverestModuleMetadata[] Multimeta { get; set; }

        /// <summary>
        /// The path to the ZIP of the mod. In case of unzipped mods, an empty string. Set at runtime.
        /// </summary>
        [YamlIgnore]
        public virtual string PathArchive { get; set; }

        /// <summary>
        /// The path to the directory of the mod. In case of .zips, an empty string. Set at runtime.
        /// </summary>
        [YamlIgnore]
        public virtual string PathDirectory { get; set; }

        /// <summary>
        /// The name of the mod.
        /// </summary>
        public virtual string Name { get; set; }

        /// <summary>
        /// The icon of the mod to be used in the mod menu.
        /// Everest loads icon.png by default, but this can also be set by the mod at runtime.
        /// </summary>
        public virtual Texture2D Icon { get; set; }

        /// <summary>
        /// The mod version.
        /// </summary>
        [YamlIgnore]
        public virtual Version Version { get; set; } = new Version(1, 0);
        protected string _VersionString;
        [YamlMember(Alias = "Version")]
        public virtual string VersionString {
            get {
                return _VersionString;
            }
            set {
                _VersionString = value;
                int versionSplitIndex = value.IndexOf('-');
                if (versionSplitIndex == -1)
                    Version = new Version(value);
                else
                    Version = new Version(value.Substring(0, versionSplitIndex));
            }
        }

        /// <summary>
        /// The path of the mod .dll inside the ZIP or the absolute DLL path if in a directory.
        /// </summary>
        public virtual string DLL { get; set; }

        /// <summary>
        /// The dependencies of the mod.
        /// </summary>
        public virtual List<EverestModuleMetadata> Dependencies { get; set; } = new List<EverestModuleMetadata>();

        /// <summary>
        /// The runtime mod hash. Might not be determined by all mod content.
        /// </summary>
        public virtual byte[] Hash { get; set; }

        public override string ToString() {
            return Name + " " + Version;
        }

        /// <summary>
        /// Perform a few basic post-parsing operations. For example, make the DLL path absolute if the mod is in a directory.
        /// </summary>
        public void PostParse() {
            if (!string.IsNullOrEmpty(DLL) && !string.IsNullOrEmpty(PathDirectory) && !File.Exists(DLL))
                DLL = Path.Combine(PathDirectory, DLL.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));

            // Add dependency to API 1.0 if missing.
            bool dependsOnAPI = false;
            foreach (EverestModuleMetadata dep in Dependencies) {
                if (dep.Name == "API")
                    dep.Name = CoreModule.Instance.Metadata.Name;
                if (dep.Name == CoreModule.Instance.Metadata.Name) {
                    dependsOnAPI = true;
                    break;
                }
            }
            if (!dependsOnAPI) {
                // Logger.Log(LogLevel.Warn, "loader", $"No dependency to API found in {this}! Adding dependency to {CoreModule.Instance.Metadata}");
                Dependencies.Insert(0, CoreModule.Instance.Metadata);
            }
        }

    }
}
