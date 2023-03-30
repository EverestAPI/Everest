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
            Logger.Log(LogLevel.Warn, "Content", "XML File not found: " + filename);
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
            Logger.Log(LogLevel.Warn, "Content", "XML File not found: " + filename);
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

    }
}
