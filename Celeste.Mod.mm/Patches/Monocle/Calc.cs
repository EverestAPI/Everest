#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;
using System.IO;
using System.Xml;

namespace Monocle {
    static class patch_Calc {

        // Allow loading empty XmlDocuments for originally non-existent content.

        public static extern XmlDocument orig_LoadContentXML(string filename);
        public static XmlDocument LoadContentXML(string filename) {
            if (Everest.Content.TryGet<AssetTypeXml>(filename.Substring(0, filename.Length - 4), out ModAsset asset)) {
                XmlDocument doc = new XmlDocument();
                using (Stream stream = asset.Stream)
                    doc.Load(stream);
                return doc;
            }
            if (orig_ContentXMLExists(filename))
                return orig_LoadContentXML(filename);
            return new XmlDocument();
        }

        public static extern XmlDocument orig_LoadXML(string filename);
        public static XmlDocument LoadXML(string filename) {
            if (Everest.Content.TryGet<AssetTypeXml>(filename.Substring(0, filename.Length - 4), out ModAsset asset)) {
                XmlDocument doc = new XmlDocument();
                using (Stream stream = asset.Stream)
                    doc.Load(stream);
                return doc;
            }
            if (orig_XMLExists(filename))
                return orig_LoadXML(filename);
            return new XmlDocument();
        }

        public static extern bool orig_ContentXMLExists(string filename);
        public static bool ContentXMLExists(string filename) {
            return true;
        }

        public static extern bool orig_XMLExists(string filename);
        public static bool XMLExists(string filename) {
            return true;
        }

        [MonoModReplace]
        public static float Percent(float num, float zeroAt, float oneAt) {
            // return MathHelper.Clamp((num - zeroAt) / oneAt, 0f, 1f);
            return MathHelper.Clamp((num - zeroAt) / (oneAt - zeroAt), 0f, 1f);
        }

        /// <summary>
        /// Convert a hex color, possibly including an alpha value, into an XNA Color.
        /// </summary>
        /// <param name="hex">a hex color, in either <c>RRGGBB</c> or <c>RRGGBBAA</c> form.</param>
        /// <returns>an XNA color, defaulting to white.</returns>
        public static Color HexToColorWithAlpha(string hex) {
            int consumed = 0;

            if (hex.Length >= 1 && hex[0] == '#') {
                consumed = 1;
            }

            int r, g, b, a;

            switch (hex.Length - consumed) {
                case 6:
                    // three bytes, for RGB and no alpha
                    r = Calc.HexToByte(hex[consumed++]) * 16 + Calc.HexToByte(hex[consumed++]);
                    g = Calc.HexToByte(hex[consumed++]) * 16 + Calc.HexToByte(hex[consumed++]);
                    b = Calc.HexToByte(hex[consumed++]) * 16 + Calc.HexToByte(hex[consumed++]);
                    return new Color(r, g, b);

                case 8:
                    // four bytes, filling all four channels
                    r = Calc.HexToByte(hex[consumed++]) * 16 + Calc.HexToByte(hex[consumed++]);
                    g = Calc.HexToByte(hex[consumed++]) * 16 + Calc.HexToByte(hex[consumed++]);
                    b = Calc.HexToByte(hex[consumed++]) * 16 + Calc.HexToByte(hex[consumed++]);
                    a = Calc.HexToByte(hex[consumed++]) * 16 + Calc.HexToByte(hex[consumed++]);
                    return new Color(r, g, b, a);

                default:
                    // some invalid data, so return a sensible default
                    return Color.White;
            }
        }

    }
}
