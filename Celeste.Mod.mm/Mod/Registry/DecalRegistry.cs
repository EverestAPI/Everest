using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Celeste.Mod {
    /// <summary>
    /// Allows custom decals to have properties that are otherwise hardcoded, such as reflections, parallax, etc.
    /// </summary>
    public static class DecalRegistry {

        public static Dictionary<string, DecalInfo> RegisteredDecals = new Dictionary<string, DecalInfo>() {

        };
        
        /// <summary>
        /// Reads a DecalRegistry.xml file
        /// </summary>
        public static void ReadXml(string fileContents) {
            //XmlElement file = Calc.LoadXML(path)["decals"];
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(fileContents);
            XmlElement file = doc["decals"];
            foreach (XmlNode node in file) {
                if (node is XmlElement decal) {
                    string decalPath = decal.Attr("path", null);
                    if (decalPath == null) {
                        Logger.Log(LogLevel.Warn, "Decal Registry", "Decal didn't have a path attribute!");
                        continue;
                    }
                    DecalInfo info = new DecalInfo();
                    info.CustomProperties = new Dictionary<string, XmlAttributeCollection>();
                    info.Depth = decal.AttrInt("depth", -1);
                    info.AnimationSpeed = decal.AttrFloat("animationSpeed", -1f);
                    // Read all the properties
                    foreach (XmlNode node2 in decal.ChildNodes) {
                        if (node2 is XmlElement property) {
                            switch (property.Name) {
                                case "parallax":
                                    info.ParallaxAmt = property.AttrFloat("amount", 0f);
                                    break;
                                case "smoke":
                                    info.Smoke = true;
                                    info.SmokeOffset = property.AttrVector2("offsetX", "offsetY", Vector2.Zero);
                                    info.SmokeInBg = property.AttrBool("inbg", false);
                                    break;
                                case "mirror":
                                    info.Mirror = true;
                                    info.MirrorKeepOffsetsClose = property.AttrBool("keepOffsetsClose", false);
                                    break;
                                case "floaty":
                                    info.Floaty = true;
                                    break;
                                case "banner":
                                    info.Banner = true;
                                    info.BannerAmplitude = property.AttrFloat("amplitude", 2f);
                                    info.BannerEaseDown = property.AttrBool("easeDown", false);
                                    info.BannerOffset = property.AttrFloat("offset", 0f);
                                    info.BannerOnlyIfWindy = property.AttrBool("onlyIfWindy", false);
                                    info.BannerSliceSinIncrement = property.AttrFloat("sliceSinIncrement", 0.05f);
                                    info.BannerSliceSize = property.AttrInt("sliceSize", 1);
                                    info.BannerSpeed = property.AttrFloat("speed", 2f);
                                    break;
                                case "coreSwap":
                                    info.CoreSwap = true;
                                    info.CoreSwapColdPath = property.Attr("coldPath", decalPath);
                                    info.CoreSwapHotPath = property.Attr("hotPath", decalPath);
                                    break;
                                case "bloom":
                                    info.Bloom = true;
                                    info.BloomAlpha = property.AttrFloat("alpha", 1f);
                                    info.BloomOffset = property.AttrVector2("offsetX", "offsetY", Vector2.Zero);
                                    info.BloomRadius = property.AttrFloat("radius", 16f);
                                    break;
                                case "sound":
                                    info.Sound = property.Attr("event", null);
                                    break;
                                default:
                                    // Unrecognized property, might still be used for mods
                                    info.CustomProperties.Add(property.Name, property.Attributes);
                                    break;
                            }
                        }
                    }
                    Logger.Log("Decal Registry", $"Registered decal {decalPath}");
                    RegisteredDecals.Add(decalPath, info);
                }
            }
        }

        public struct DecalInfo {
            // parallax
            public float ParallaxAmt;
            // smoke
            public bool Smoke;
            public Vector2 SmokeOffset;
            public bool SmokeInBg;
            // mirror
            public bool Mirror;
            public bool MirrorKeepOffsetsClose;
            // floaty
            public bool Floaty;
            // banner
            public bool Banner;
            public float BannerSpeed;
            public float BannerAmplitude;
            public int BannerSliceSize;
            public float BannerSliceSinIncrement;
            public bool BannerEaseDown;
            public float BannerOffset;
            public bool BannerOnlyIfWindy;
            // depth
            public int Depth;
            // coreSwap
            public bool CoreSwap;
            public string CoreSwapColdPath;
            public string CoreSwapHotPath;
            // bloom
            public bool Bloom;
            public Vector2 BloomOffset;
            public float BloomAlpha;
            public float BloomRadius;
            // sound
            public string Sound;
            // animationSpeed
            public float AnimationSpeed;
            /// <summary>
            /// These are not recognized by Everest, but still may be used by mods.
            /// PropertyName -> AttributeCollection
            /// </summary>
            public Dictionary<string, XmlAttributeCollection> CustomProperties;
        }
    }
}
