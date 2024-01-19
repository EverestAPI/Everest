#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using MonoMod;
using System;
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
            if (XMLPath != null) {
                Everest.Content.ProcessLoad(this, XMLPath);
            }
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
            XmlDocument spriteBankXml;
            XmlDocument originalSpriteBankXml;
            if (patch_Calc.orig_ContentXMLExists(filename)) {
                spriteBankXml = patch_Calc.orig_LoadContentXML(filename);
                originalSpriteBankXml = patch_Calc.orig_LoadContentXML(filename);
            } else {
                // For any mods that load their own SpriteBanks
                return Calc.LoadContentXML(filename);
            }

            XmlElement sprites = spriteBankXml["Sprites"];

            string modAssetPath = filename.Substring(0, filename.Length - 4).Replace('\\', '/');

            // Find all mod files that match this one, EXCEPT for the "shadow structure" asset - the unique "Graphics/Sprites" asset.
            List<ModAsset> modAssets;
            lock (Everest.Content.Map)
                modAssets = Everest.Content.Map
                    .Where((a) => a.Value.Type == typeof(AssetTypeSpriteBank) &&
                        a.Value.PathVirtual.Equals(modAssetPath) &&
                        !a.Value.PathVirtual.Equals(a.Key)) // Filter out the unique asset
                    .Select(kvp => kvp.Value)
                    .ToList();

            foreach (ModAsset modAsset in modAssets) {
                string modPath = modAsset.Source.Mod.PathDirectory;
                if (string.IsNullOrEmpty(modPath))
                    modPath = modAsset.Source.Mod.PathArchive;

                using (Stream stream = modAsset.Stream) {
                    XmlDocument modXml = new XmlDocument();
                    modXml.Load(stream);
                    modXml = GetSpriteBankExcludingVanillaCopyPastes(originalSpriteBankXml, modXml, modPath);

                    foreach (XmlNode node in modXml["Sprites"].ChildNodes) {
                        if (!(node is XmlElement))
                            continue;

                        XmlNode importedNode = spriteBankXml.ImportNode(node, true);

                        XmlNode existingNode = sprites.SelectSingleNode(node.Name);
                        if (existingNode != null) {
                            // Unfortuately we don't know what spritebank added the element that's being replaced
                            Logger.Warn("Content", $"CONFLICT in {modPath}{Path.DirectorySeparatorChar}{filename}: Overriding element {node.Name}.");
                            sprites.ReplaceChild(importedNode, existingNode);
                        } else
                            sprites.AppendChild(importedNode);
                    }
                }
            }
            return spriteBankXml;
        }

        /// <summary>
        /// Returns a mod SpriteBank with all sprites copy-pasted and unmodified from the vanilla SpriteBank filtered out.
        /// This allows to minimize mod conflicts.
        /// </summary>
        /// <param name="vanillaSpritesXml">The content of the vanilla SpriteBank</param>
        /// <param name="modSpritesXml">The content of the mod SpriteBank</param>
        /// <param name="path">The path to the SpriteBank file, for logging</param>
        /// <returns>The mod SpriteBank with all sprites identical to vanilla filtered out.</returns>
        internal static XmlDocument GetSpriteBankExcludingVanillaCopyPastes(XmlDocument vanillaSpritesXml, XmlDocument modSpritesXml, string path) {
            XmlNode vanillaSprites = vanillaSpritesXml["Sprites"];
            XmlNode modSprites = modSpritesXml["Sprites"];

            List<XmlNode> pendingDeletion = new List<XmlNode>();
            List<string> doNotDelete = new List<string>();

            // go through all the sprites.
            foreach (XmlNode modNode in getChildElements(modSprites)) {
                XmlNode vanillaNode = vanillaSprites.SelectSingleNode(modNode.Name);
                if (vanillaNode != null) {
                    if (xmlNodesAreIdentical(vanillaNode, modNode)) {
                        // mod XML identical to vanilla, mark it for deletion.
                        pendingDeletion.Add(modNode);
                    } else {
                        // mod XML different from vanilla, keep it.
                        Logger.Verbose("SpriteBank", $"Sprite \"{modNode.Name}\" will be overridden with {path}.");

                        // if it copies another sprite, keep its name so that we do not delete it.
                        string copy = modNode.Attributes["copy"]?.Value;
                        if (copy != null) {
                            doNotDelete.Add(copy);
                        }
                    }
                } else {
                    // sprite doesn't exist in vanilla.
                    Logger.Verbose("SpriteBank", $"Sprite \"{modNode.Name}\" will be added from {path}.");
                }
            }

            // delete all sprites marked for deletion, except ones that are copied by sprites we want to keep (because this would break the XML).
            foreach (XmlNode toDelete in pendingDeletion.ToArray()) {
                if (doNotDelete.Contains(toDelete.Name)) {
                    Logger.Verbose("SpriteBank", $"Sprite \"{toDelete.Name}\" will be overridden with {path}, because it is copied by another sprite.");
                } else {
                    modSprites.RemoveChild(toDelete);
                }
            }

            return modSpritesXml;
        }

        /// <summary>
        /// Checks if the 2 given nodes are identical (same attributes and same child elements).
        /// </summary>
        /// <param name="node1">The first node to compare</param>
        /// <param name="node2">The second node to compare</param>
        /// <returns>true if the nodes match up, false otherwise.</returns>
        private static bool xmlNodesAreIdentical(XmlNode node1, XmlNode node2) {
            // compare attributes
            if (node1.Attributes.Count != node2.Attributes.Count) {
                return false;
            }
            for (int i = 0; i < node1.Attributes.Count; i++) {
                if (node1.Attributes[i].Name != node2.Attributes[i].Name || node1.Attributes[i].Value != node2.Attributes[i].Value) {
                    return false;
                }
            }

            // compare child elements
            List<XmlNode> node1List = getChildElements(node1);
            List<XmlNode> node2List = getChildElements(node2);
            if (node1List.Count != node2List.Count) {
                return false;
            }
            for (int i = 0; i < node1List.Count; i++) {
                if (!xmlNodesAreIdentical(node1List[i], node2List[i])) {
                    return false;
                }
            }

            // everything matches!
            return true;
        }

        /// <summary>
        /// Gets a list of child <b>elements</b> (excluding comments, text, etc) for the given node.
        /// </summary>
        /// <param name="node">The node to get the children from</param>
        /// <returns>The list of child elements</returns>
        private static List<XmlNode> getChildElements(XmlNode node) {
            List<XmlNode> result = new List<XmlNode>();
            foreach (XmlNode childNode in node.ChildNodes) {
                if (childNode is XmlElement) {
                    result.Add(childNode);
                }
            }
            return result;
        }

    }
    public static class SpriteBankExt {

        /// <summary>
        /// Get the path to the file from which the SpriteBank was loaded.
        /// </summary>
        [Obsolete("Use SpriteBank.XMLPath instead.")]
        public static string GetXMLPath(this SpriteBank self)
            => ((patch_SpriteBank) self).XMLPath;

    }
}
