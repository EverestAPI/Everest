using Celeste.Mod;
using Celeste.Mod.Helpers;
using MonoMod;
using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace Monocle {
    public class patch_PixelFont : PixelFont {
        public patch_PixelFont(string face) : base(face) {
            // this constructor is ignored
        }

        [MonoModReplace]
        public new PixelFontSize AddFontSize(string path, Atlas atlas = null, bool outline = false) {
            Logger.Verbose("PixelFont", $"Loading font: {path}");

            PixelFontSize loadedSize = null;

            // load the vanilla font if it exists.
            if (patch_Calc.orig_XMLExists(path)) {
                Logger.Verbose("PixelFont", "=> vanilla font file");
                XmlElement data = patch_Calc.orig_LoadXML(path)["font"];
                loadedSize = AddFontSize(path, data, atlas, outline);
                Sizes.Remove(loadedSize);
            }

            // load custom fonts
            string modPath = FileProxy._Modize(path);
            foreach (ModAsset modAsset in Everest.Content.Mods
                .Select(mod => mod.Map)
                .Where(map => map.ContainsKey(modPath))
                .Select(map => map[modPath])) {

                Logger.Verbose("PixelFont", $"=> mod font file from {modAsset.Source.Name}");
                XmlElement data = loadXMLFromModAsset(modAsset)["font"];
                PixelFontSize newFontSize = AddFontSize(path, data, atlas, outline);
                Sizes.Remove(newFontSize);
                loadedSize = mergeFonts(loadedSize, newFontSize);
            }

            // add the merged font into the list of existing fonts.
            Sizes.Add(loadedSize);
            Sizes.Sort((PixelFontSize a, PixelFontSize b) => Math.Sign(a.Size - b.Size));

            return loadedSize;
        }

        private XmlDocument loadXMLFromModAsset(ModAsset modAsset) {
            XmlDocument xmlDocument = new XmlDocument();
            using (Stream inStream = modAsset.Stream) {
                xmlDocument.Load(inStream);
                return xmlDocument;
            }
        }

        private PixelFontSize mergeFonts(PixelFontSize originalSize, PixelFontSize newSize) {
            if (originalSize == null) {
                // nothing to merge, the current font size does not exist.
                return newSize;
            }

            int newCharacterCount = 0;
            foreach (int character in newSize.Characters.Keys) {
                // add new characters into the existing font.
                if (!originalSize.Characters.ContainsKey(character)) {
                    originalSize.Characters[character] = newSize.Characters[character];
                    newCharacterCount++;
                }
            }

            // associate the textures for the new font to the existing font.
            originalSize.Textures.AddRange(newSize.Textures);

            Logger.Verbose("PixelFont", $"==> Imported {newCharacterCount} new characters");
            return originalSize;
        }
    }
}
