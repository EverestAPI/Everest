using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace Celeste.Mod {
    /// <summary>
    /// Allows custom decals to have properties that are otherwise hardcoded, such as reflections, parallax, etc.
    /// </summary>
    public static class DecalRegistry {

        // string is propertyName
        public static Dictionary<string, Action<Decal, XmlAttributeCollection>> PropertyHandlers = new Dictionary<string, Action<Decal, XmlAttributeCollection>>() {
            { "parallax", delegate(Decal decal, XmlAttributeCollection attrs) {
                if (attrs["amount"] != null)
                    ((patch_Decal)decal).MakeParallax(float.Parse(attrs["amount"].Value));
            }},
            { "scale", delegate(Decal decal, XmlAttributeCollection attrs) {
                float scaleX = attrs["multiplyX"] != null ? float.Parse(attrs["multiplyX"].Value) : 1f;
                float scaleY = attrs["multiplyY"] != null ? float.Parse(attrs["multiplyY"].Value) : 1f;
                ((patch_Decal)decal).Scale *= new Vector2(scaleX, scaleY);
            }},
            { "smoke", delegate(Decal decal, XmlAttributeCollection attrs) {
                float offX = attrs["offsetX"] != null ? float.Parse(attrs["offsetX"].Value) : 0f;
                float offY = attrs["offsetY"] != null ? float.Parse(attrs["offsetY"].Value) : 0f;
                bool inbg = attrs["inbg"] != null ? bool.Parse(attrs["inbg"].Value) : false;

                Vector2 offset = ScaleOffset(((patch_Decal)decal).Scale, offX, offY);

                ((patch_Decal)decal).CreateSmoke(offset, inbg);
            }},
            { "depth", delegate(Decal decal, XmlAttributeCollection attrs) {
                if (attrs["value"] != null)
                    decal.Depth = int.Parse(attrs["value"].Value);
            }},
            { "animationSpeed", delegate(Decal decal, XmlAttributeCollection attrs) {
                if (attrs["value"] != null)
                    decal.AnimationSpeed = int.Parse(attrs["value"].Value);
            }},
            { "floaty", delegate(Decal decal, XmlAttributeCollection attrs) {
                ((patch_Decal)decal).MakeFloaty();
            }},
            { "sound", delegate(Decal decal, XmlAttributeCollection attrs) {
                if (attrs["event"] != null)
                    decal.Add(new SoundSource(attrs["event"].Value));
            }},
            { "bloom", delegate(Decal decal, XmlAttributeCollection attrs) {
                float offX = attrs["offsetX"] != null ? float.Parse(attrs["offsetX"].Value) : 0f;
                float offY = attrs["offsetY"] != null ? float.Parse(attrs["offsetY"].Value) : 0f;
                float alpha = attrs["alpha"] != null ? float.Parse(attrs["alpha"].Value) : 1f;
                float radius = attrs["radius"] != null ? float.Parse(attrs["radius"].Value) : 1f;

                Vector2 offset = ScaleOffset(((patch_Decal)decal).Scale, offX, offY);
                radius = ScaleRadius(((patch_Decal)decal).Scale, radius);

                decal.Add(new BloomPoint(offset, alpha, radius));
            }},
            { "coreSwap", delegate(Decal decal, XmlAttributeCollection attrs) {
                ((patch_Decal)decal).MakeFlagSwap("cold", attrs["hotPath"]?.Value, attrs["coldPath"]?.Value);
            }},
            { "mirror", delegate(Decal decal, XmlAttributeCollection attrs) {
                string text = decal.Name.ToLower();
                if (text.StartsWith("decals/"))
                    text = text.Substring(7);
                bool keepOffsetsClose = attrs["keepOffsetsClose"] != null ? bool.Parse(attrs["keepOffsetsClose"].Value) : false;
                ((patch_Decal)decal).MakeMirror(text, keepOffsetsClose);
            }},
            { "banner", delegate(Decal decal, XmlAttributeCollection attrs) {
                float speed = attrs["speed"] != null ? float.Parse(attrs["speed"].Value) : 1f;
                float amplitude = attrs["amplitude"] != null ? float.Parse(attrs["amplitude"].Value) : 1f;
                int sliceSize = attrs["sliceSize"] != null ? int.Parse(attrs["sliceSize"].Value) : 1;
                float sliceSinIncrement = attrs["sliceSinIncrement"] != null ? float.Parse(attrs["sliceSinIncrement"].Value) : 1f;
                bool easeDown = attrs["easeDown"] != null ? bool.Parse(attrs["easeDown"].Value) : false;
                float offset = attrs["offset"] != null ? float.Parse(attrs["offset"].Value) : 0f;
                bool onlyIfWindy = attrs["onlyIfWindy"] != null ? bool.Parse(attrs["onlyIfWindy"].Value): false;

                amplitude *= ((patch_Decal)decal).Scale.X;
                offset *= Math.Sign(((patch_Decal)decal).Scale.X) * Math.Abs(((patch_Decal)decal).Scale.Y);

                ((patch_Decal)decal).MakeBanner(speed, amplitude, sliceSize, sliceSinIncrement, easeDown, offset, onlyIfWindy);
            }},
            { "solid", delegate(Decal decal, XmlAttributeCollection attrs) {
                float x = attrs["x"] != null ? float.Parse(attrs["x"].Value) : 0f;
                float y = attrs["y"] != null ? float.Parse(attrs["y"].Value) : 0f;
                float width = attrs["width"] != null ? float.Parse(attrs["width"].Value) : 16f;
                float height = attrs["height"] != null ? float.Parse(attrs["height"].Value) : 16f;
                int index = attrs["index"] != null ? int.Parse(attrs["index"].Value) : SurfaceIndex.ResortRoof;
                bool blockWaterfalls = attrs["blockWaterfalls"] != null ? bool.Parse(attrs["blockWaterfalls"].Value) : true;
                bool safe = attrs["safe"] != null ? bool.Parse(attrs["safe"].Value) : true;

                ScaleRectangle(((patch_Decal)decal).Scale, ref x, ref y, ref width, ref height);

                ((patch_Decal)decal).MakeSolid(x, y, width, height, index, blockWaterfalls, safe);
            }},
            { "staticMover", delegate(Decal decal, XmlAttributeCollection attrs) {
                int x = attrs["x"] != null ? int.Parse(attrs["x"].Value) : 0;
                int y = attrs["y"] != null ? int.Parse(attrs["y"].Value) : 0;
                int width = attrs["width"] != null ? int.Parse(attrs["width"].Value) : 16;
                int height = attrs["height"] != null ? int.Parse(attrs["height"].Value) : 16;

                ScaleRectangle(((patch_Decal)decal).Scale, ref x, ref y, ref width, ref height);

                ((patch_Decal)decal).MakeStaticMover(x, y, width, height);
            }},
            { "scared", delegate(Decal decal, XmlAttributeCollection attrs) {
                int hideRange = 32;
                int showRange = 48;
                if (attrs["range"] != null)
                    hideRange = showRange = int.Parse(attrs["range"].Value);
                if (attrs["hideRange"] != null)
                    hideRange = int.Parse(attrs["hideRange"].Value);
                if (attrs["showRange"] != null)
                    showRange = int.Parse(attrs["showRange"].Value);
                int[] idleFrames = Calc.ReadCSVIntWithTricks(attrs["idleFrames"]?.Value ?? "0");
                int[] hiddenFrames = Calc.ReadCSVIntWithTricks(attrs["hiddenFrames"]?.Value ?? "0");
                int[] hideFrames = Calc.ReadCSVIntWithTricks(attrs["hideFrames"]?.Value ?? "0");
                int[] showFrames = Calc.ReadCSVIntWithTricks(attrs["showFrames"]?.Value ?? "0");

                hideRange = (int) ScaleRadius(((patch_Decal)decal).Scale, hideRange);
                showRange = (int) ScaleRadius(((patch_Decal)decal).Scale, showRange);

                ((patch_Decal)decal).MakeScaredAnimation(hideRange, showRange, idleFrames, hiddenFrames, showFrames, hideFrames);
            }},
            { "randomizeFrame", delegate(Decal decal, XmlAttributeCollection attrs) {
                ((patch_Decal)decal).RandomizeStartingFrame();
            }},
            { "light", delegate(Decal decal, XmlAttributeCollection attrs) {
                float offX = attrs["offsetX"] != null ? float.Parse(attrs["offsetX"].Value) : 0f;
                float offY = attrs["offsetY"] != null ? float.Parse(attrs["offsetY"].Value) : 0f;
                Color color = attrs["color"] != null ? Calc.HexToColor(attrs["color"].Value) : Color.White;
                float alpha = attrs["alpha"] != null ? float.Parse(attrs["alpha"].Value) : 1f;
                int startFade = attrs["startFade"] != null ? int.Parse(attrs["startFade"].Value) : 16;
                int endFade = attrs["endFade"] != null ? int.Parse(attrs["endFade"].Value) : 24;

                Vector2 offset = ScaleOffset(((patch_Decal)decal).Scale, offX, offY);
                startFade = (int) ScaleRadius(((patch_Decal)decal).Scale, startFade);
                endFade = (int) ScaleRadius(((patch_Decal)decal).Scale, endFade);

                decal.Add(new VertexLight(offset, color, alpha, startFade, endFade));
            }},
            { "lightOcclude", delegate(Decal decal, XmlAttributeCollection attrs) {
                int x = attrs["x"] != null ? int.Parse(attrs["x"].Value) : 0;
                int y = attrs["y"] != null ? int.Parse(attrs["y"].Value) : 0;
                int width = attrs["width"] != null ? int.Parse(attrs["width"].Value) : 16;
                int height = attrs["height"] != null ? int.Parse(attrs["height"].Value) : 16;
                float alpha = attrs["alpha"] != null ? float.Parse(attrs["alpha"].Value) : 1f;

                ScaleRectangle(((patch_Decal)decal).Scale, ref x, ref y, ref width, ref height);

                decal.Add(new LightOcclude(new Rectangle(x, y, width, height), alpha));
            }},
            { "overlay", delegate(Decal decal, XmlAttributeCollection attrs) {
                ((patch_Decal)decal).MakeOverlay();
            }},
            { "flagSwap", delegate(Decal decal, XmlAttributeCollection attrs) {
                if (attrs["flag"] != null)
                    ((patch_Decal)decal).MakeFlagSwap(attrs["flag"].Value, attrs["offPath"]?.Value, attrs["onPath"]?.Value);
            }},
        };

        public static Vector2 ScaleOffset(Vector2 scale, float x, float y) {
            return new Vector2(x * scale.X, y * scale.Y);
        }

        public static float ScaleRadius(Vector2 scale, float radius) { 
            return radius * ((Math.Abs(scale.X) + Math.Abs(scale.Y)) / 2f);
        }

        public static void ScaleRectangle(Vector2 scale, ref float x, ref float y, ref float width, ref float height) {
            x *= Math.Abs(scale.X);
            y *= Math.Abs(scale.Y);
            width *= Math.Abs(scale.X);
            height *= Math.Abs(scale.Y);

            x = (scale.X < 0) ? -x - width : x;
            y = (scale.Y < 0) ? -y - height : y;
        }

        public static void ScaleRectangle(Vector2 scale, ref int x, ref int y, ref int width, ref int height) {
            x = (int) (x * Math.Abs(scale.X));
            y = (int) (y * Math.Abs(scale.Y));
            width = (int) (width * Math.Abs(scale.X));
            height = (int) (height * Math.Abs(scale.Y));

            x = (scale.X < 0) ? -x - width : x;
            y = (scale.Y < 0) ? -y - height : y;
        }

        public static Dictionary<string, DecalInfo> RegisteredDecals = new Dictionary<string, DecalInfo>();

        public static void AddPropertyHandler(string propertyName, Action<Decal, XmlAttributeCollection> action) {
            if (PropertyHandlers.ContainsKey(propertyName)) {
                string asmName = Assembly.GetCallingAssembly().GetName().Name;
                string modName = Everest.Content.Mods
                                 .Where(mod => mod is AssemblyModContent && mod.DefaultName == asmName)
                                 .FirstOrDefault()?.Name;
                string conflictSource = !string.IsNullOrEmpty(modName) ? modName : asmName;
                Logger.Log(LogLevel.Warn, "Decal Registry", $"Property handler for '{propertyName}' already exists! Replacing with new handler from {conflictSource}.");
                PropertyHandlers[propertyName] = action;
            } else {
                PropertyHandlers.Add(propertyName, action);
            }
        }

        /// <summary>
        /// Reads a DecalRegistry.xml file's contents
        /// </summary>
        public static List<KeyValuePair<string, DecalInfo>> ReadDecalRegistryXml(string fileContents) {
            // XmlElement file = Calc.LoadXML(path)["decals"];
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(fileContents);
            XmlElement file = doc["decals"];

            List<KeyValuePair<string, DecalInfo>> elements = new();
            foreach (XmlNode node in file) {
                if (node is XmlElement decal) {
                    string decalPath = decal.Attr("path", null)?.ToLower();
                    if (decalPath == null) {
                        Logger.Log(LogLevel.Warn, "Decal Registry", "Decal didn't have a path attribute!");
                        continue;
                    }
                    DecalInfo info = new DecalInfo();
                    info.CustomProperties = new List<KeyValuePair<string, XmlAttributeCollection>>();
                    // Read all the properties
                    foreach (XmlNode node2 in decal.ChildNodes) {
                        if (node2 is XmlElement property) {
                            if (property.Attributes == null) {
                                property.SetAttribute("a", "only here to prevent crashes");
                            }
                            info.CustomProperties.Add(new KeyValuePair<string, XmlAttributeCollection>(property.Name, property.Attributes));
                        }
                    }

                    elements.Add(new KeyValuePair<string, DecalInfo>(decalPath, info));
                }
            }

            return elements;
        }

        public static void RegisterDecal(string decalPath, DecalInfo info) {
            if (RegisteredDecals.ContainsKey(decalPath)) {
                Logger.Log("Decal Registry", $"Replaced decal {decalPath}");
                RegisteredDecals[decalPath] = info;
            } else {
                Logger.Log("Decal Registry", $"Registered decal {decalPath}");
                RegisteredDecals.Add(decalPath, info);
            }
        }

        public static void LoadDecalRegistry() {
            List<KeyValuePair<string, DecalInfo>> completeDecalRegistry = new();

            foreach (ModAsset asset in
                Everest.Content.Mods
                .Select(mod => mod.Map.TryGetValue("DecalRegistry", out ModAsset asset) ? asset : null)
                .Where(asset => asset != null && asset.Type == typeof(AssetTypeDecalRegistry))
            ) {
                string fileContents;
                using (StreamReader reader = new StreamReader(asset.Stream)) {
                    fileContents = reader.ReadToEnd();
                }
                completeDecalRegistry.AddRange(ReadDecalRegistryXml(fileContents));
            }

            // We can treat this as if we only had a single, full decal registry, to keep consistency between files
            ApplyDecalRegistry(completeDecalRegistry);
        }

        public static void ApplyDecalRegistry(List<KeyValuePair<string, DecalInfo>> elements) {
            // Sort by priority in this order: folders -> matching paths -> single decal
            elements.Sort((a, b) => {
                int scoreA = a.Key[a.Key.Length - 1] switch {
                    '/' => 2,
                    '*' => 1,
                    _ => 0,
                };
                int scoreB = b.Key[b.Key.Length - 1] switch {
                    '/' => 2,
                    '*' => 1,
                    _ => 0,
                };
                // If both end with '*', then we can still be more precise and sort for the best match
                return (scoreA == scoreB && scoreA == 1) ? a.Key.Length - b.Key.Length : scoreB - scoreA;
            });

            foreach (KeyValuePair<string, DecalInfo> pair in elements) {
                string decalPath = pair.Key;
                DecalInfo info = pair.Value;
                if (decalPath.EndsWith("*") || decalPath.EndsWith("/")) {
                    // Removing the '/' made the path wrong
                    decalPath = decalPath.TrimEnd('*');
                    int pathLength = decalPath.Length;

                    foreach (string subDecalPath in
                        GFX.Game.GetTextures().Keys
                        .GroupBy(
                            s => s.StartsWith("decals/") ?
                                s.Substring(7).TrimEnd('0','1','2','3','4','5','6','7','8','9').ToLower() :
                                null,
                            (s, matches) => s
                        )
                        .Where(str => str != null && str.StartsWith(decalPath) && str.Length > pathLength)
                    ) {
                        // Decals in subfolders are considered as unmatched
                        if (!subDecalPath.Remove(0, pathLength).Contains("/"))
                            RegisterDecal(subDecalPath, info);
                    }
                } else {
                    // Single decal registered
                    RegisterDecal(decalPath, info);
                }
            }
        }

        public struct DecalInfo {
            /// <summary>
            /// PropertyName -> AttributeCollection
            /// </summary>
            public List<KeyValuePair<string, XmlAttributeCollection>> CustomProperties;
        }
    }
}
