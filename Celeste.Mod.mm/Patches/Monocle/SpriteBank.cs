#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using MonoMod;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Monocle {
    class patch_SpriteBank : SpriteBank {

        /// <summary>
        /// The path to the file from which the SpriteBank was loaded.
        /// </summary>
        public string XMLPath;

        public patch_SpriteBank(Atlas atlas, XmlDocument xml)
            : base(atlas, xml) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Patching constructors is ugly.
        public extern void orig_ctor(Atlas atlas, XmlDocument xml);
        [MonoModConstructor]
        public void ctor(Atlas atlas, XmlDocument xml) {
            orig_ctor(atlas, xml);
            Everest.Content.ProcessLoad(this, XMLPath);
        }

        [MonoModConstructor]
        [MonoModReplace]
        public void ctor(Atlas atlas, string xmlPath) {
            XMLPath = xmlPath;
            orig_ctor(atlas, LoadSpriteBank(xmlPath));
        }

        /// <summary>
        /// Load SpriteBank from file and merge mod SpriteBanks.
        /// </summary>
        /// <param name="filename">Xml file to load</param>
        /// <returns></returns>
        public static XmlDocument LoadSpriteBank(string filename) {
            XmlDocument spriteBankXml = new XmlDocument();
            if (patch_Calc.orig_ContentXMLExists(filename)) {
                spriteBankXml = patch_Calc.orig_LoadContentXML(filename);
            } else {
                //For any mods that load their own SpriteBanks
                return Calc.LoadContentXML(filename);
            }

            XmlElement sprites = spriteBankXml["Sprites"];

            string modAssetPath = filename.Substring(0, filename.Length - 4).Replace('\\', '/');

            //Find all mod files that match this one
            List<ModAsset> modAssets;
            lock (Everest.Content.Map)
                modAssets = Everest.Content.Map.Values
                    .Where(a => a.Type == typeof(AssetTypeSpriteBank) && a.PathVirtual.Equals(modAssetPath))
                    .ToList();

            foreach (ModAsset modAsset in modAssets) {
                string modPath = modAsset.Source.Mod.PathDirectory;
                if (string.IsNullOrEmpty(modPath))
                    modPath = modAsset.Source.Mod.PathArchive;

                using (Stream stream = modAsset.Stream) {
                    XmlDocument modXml = new XmlDocument();
                    modXml.Load(stream);

                    foreach (XmlNode node in modXml["Sprites"].ChildNodes) {
                        if (!(node is XmlElement))
                            continue;

                        XmlNode importedNode = spriteBankXml.ImportNode(node, true);

                        XmlNode existingNode = sprites.SelectSingleNode(node.Name);
                        if (existingNode != null) {
                            //Unfortuately we don't know what spritebank added the element that's being replaced
                            Logger.Log(LogLevel.Warn, "Content", $"CONFLICT in {modPath}{Path.DirectorySeparatorChar}{filename}: Overriding element {node.Name}.");
                            sprites.ReplaceChild(importedNode, existingNode);
                        } else
                            sprites.AppendChild(importedNode);
                    }
                }
            }
            return spriteBankXml;
        }

    }
    public static class SpriteBankExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the path to the file from which the SpriteBank was loaded.
        /// </summary>
        public static string GetXMLPath(this SpriteBank self)
            => ((patch_SpriteBank) self).XMLPath;

    }
}
