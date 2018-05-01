#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Monocle {
    static class patch_Calc {

        // Allow loading empty XmlDocuments for originally non-existent content.

        public static extern XmlDocument orig_LoadContentXML(string filename);
        public static XmlDocument LoadContentXML(string filename) {
            ModAsset asset;
            if (Everest.Content.TryGet<AssetTypeXml>(filename.Substring(0, filename.Length - 4), out asset)) {
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
            ModAsset asset;
            if (Everest.Content.TryGet<AssetTypeXml>(filename.Substring(0, filename.Length - 4), out asset)) {
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

    }
}
