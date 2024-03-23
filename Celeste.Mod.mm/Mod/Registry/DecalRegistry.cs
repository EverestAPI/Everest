using Celeste.Mod.Registry.DecalRegistryHandlers;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace Celeste.Mod {
    /// <summary>
    /// Allows custom decals to have properties that are otherwise hardcoded, such as reflections, parallax, etc.
    /// </summary>
    public static class DecalRegistry {
        /// <summary>
        /// Mapping of decal paths to decal registry properties.
        /// </summary>
        public static readonly Dictionary<string, DecalInfo> RegisteredDecals = new();

        /// <summary>
        /// Stores factory methods which create DecalRegistryHandler instances
        /// </summary>
        internal static Dictionary<string, Func<DecalRegistryHandler>> PropertyHandlerFactories { get; } = new();

        /// <summary>
        /// Stores whether Everest's DecalRegistryHandlers have been registered already.
        /// </summary>
        internal static bool EverestHandlersRegistered;
        
        /// <summary>
        /// Adds a custom property to the decal registry. See the Celeste.Mod.Registry.DecalRegistryHandlers namespace to see Everest-defined properties.
        /// </summary>
        [Obsolete("Use AddPropertyHandler<T>() instead")]
        public static void AddPropertyHandler(string propertyName, Action<Decal, XmlAttributeCollection> action) {
            if (PropertyHandlerFactories.ContainsKey(propertyName)) {
                LogConflict(propertyName, Assembly.GetCallingAssembly());
            }
            
            string asmName = action.Method.DeclaringType?.Assembly.GetName().Name ?? "";
            Logger.Log(LogLevel.Warn, "Decal Registry", $"Assembly {asmName} is using the legacy DecalRegistry.AddPropertyHandler(string, Action<Decal, XmlAttributeCollection>) method for property {propertyName}!");
            
            PropertyHandlerFactories[propertyName] = () => new LegacyDecalRegistryHandler(propertyName, action);
        }

        /// <summary>
        /// Adds a custom property to the decal registry. See the Celeste.Mod.Registry.DecalRegistryHandlers namespace to see Everest-defined properties.
        /// </summary>
        /// <typeparam name="T">The type of DecalRegistryHandler to use</typeparam>
        public static void AddPropertyHandler<T>() where T : DecalRegistryHandler, new() {
            var dummyHandler = new T();
            if (PropertyHandlerFactories.ContainsKey(dummyHandler.Name)) {
                LogConflict(dummyHandler.Name, Assembly.GetCallingAssembly());
            }
            
            PropertyHandlerFactories[dummyHandler.Name] = () => new T();
        }

        internal static DecalRegistryHandler CreateHandlerOrNull(string decalName, string propertyName, XmlAttributeCollection xmlAttributes) {
            if (!PropertyHandlerFactories.TryGetValue(propertyName, out var factory)) {
                Logger.Log(LogLevel.Warn, "Decal Registry", $"Unknown property {propertyName} in decal {decalName}");
                return null;
            }
            
            var handler = factory();
            handler.Parse(xmlAttributes);
            return handler;
        }

        private static void LogConflict(string propertyName, Assembly callingAsm) {
            string asmName = callingAsm.GetName().Name;
            string modName = Everest.Content.Mods.FirstOrDefault(mod => mod is AssemblyModContent && mod.DefaultName == asmName)?.Name;
            string conflictSource = !string.IsNullOrEmpty(modName) ? modName : asmName;
            Logger.Log(LogLevel.Warn, "Decal Registry", $"Property handler for '{propertyName}' already exists! Replacing with new handler from {conflictSource}.");
        }

        /// <summary>
        /// Returns a vector offset scaled relative to a decal.
        /// </summary>
        public static Vector2 GetScaledOffset(this Decal self, float x, float y) {
            return new Vector2(x * ((patch_Decal) self).Scale.X, y * ((patch_Decal) self).Scale.Y);
        }

        /// <summary>
        /// Returns a radius scaled relative to a decal.
        /// </summary>
        public static float GetScaledRadius(this Decal self, float radius) {
            return radius * ((Math.Abs(((patch_Decal) self).Scale.X) + Math.Abs(((patch_Decal) self).Scale.Y)) / 2f);
        }

        /// <summary>
        /// Returns the components of a rectangle scaled relative to a decal (float).
        /// </summary>
        public static void ScaleRectangle(this Decal self, ref float x, ref float y, ref float width, ref float height) {
            Vector2 scale = ((patch_Decal) self).Scale;
            x *= Math.Abs(scale.X);
            y *= Math.Abs(scale.Y);
            width *= Math.Abs(scale.X);
            height *= Math.Abs(scale.Y);

            x = (scale.X < 0) ? -x - width : x;
            y = (scale.Y < 0) ? -y - height : y;
        }

        /// <summary>
        /// Returns the components of a rectangle scaled relative to a decal (int).
        /// </summary>
        public static void ScaleRectangle(this Decal self, ref int x, ref int y, ref int width, ref int height) {
            Vector2 scale = ((patch_Decal) self).Scale;
            x = (int) (x * Math.Abs(scale.X));
            y = (int) (y * Math.Abs(scale.Y));
            width = (int) (width * Math.Abs(scale.X));
            height = (int) (height * Math.Abs(scale.Y));

            x = (scale.X < 0) ? -x - width : x;
            y = (scale.Y < 0) ? -y - height : y;
        }

        /// <summary>
        /// Loads the decal registry for every enabled mod.
        /// </summary>
        internal static void LoadDecalRegistry() {
            RegisterEverestHandlers();
            
            foreach (ModContent mod in Everest.Content.Mods) {
                if (mod.Map.TryGetValue("DecalRegistry", out ModAsset asset) && asset.Type == typeof(AssetTypeDecalRegistry)) {
                    LoadModDecalRegistry(asset);
                }
            }
        }

        internal static void RegisterEverestHandlers() {
            if (EverestHandlersRegistered) {
                return;
            }

            EverestHandlersRegistered = true;
            
            AddPropertyHandler<AnimationDecalRegistryHandler>();
            AddPropertyHandler<AnimationSpeedDecalRegistryHandler>();
            AddPropertyHandler<BannerDecalRegistryHandler>();
            AddPropertyHandler<BloomDecalRegistryHandler>();
            AddPropertyHandler<CoreSwapDecalRegistryHandler>();
            AddPropertyHandler<DepthDecalRegistryHandler>();
            AddPropertyHandler<FlagSwapDecalRegistryHandler>();
            AddPropertyHandler<FloatyDecalRegistryHandler>();
            AddPropertyHandler<LightDecalRegistryHandler>();
            AddPropertyHandler<LightOccludeDecalRegistryHandler>();
            AddPropertyHandler<MirrorDecalRegistryHandler>();
            AddPropertyHandler<OverlayDecalRegistryHandler>();
            AddPropertyHandler<ParallaxDecalRegistryHandler>();
            AddPropertyHandler<RandomizeFrameDecalRegistryHandler>();
            AddPropertyHandler<ScaleDecalRegistryHandler>();
            AddPropertyHandler<ScaredDecalRegistryHandler>();
            AddPropertyHandler<SmokeDecalRegistryHandler>();
            AddPropertyHandler<SolidDecalRegistryHandler>();
            AddPropertyHandler<SoundDecalRegistryHandler>();
            AddPropertyHandler<StaticMoverDecalRegistryHandler>();
        }

        /// <summary>
        /// Loads a mod's decal registry file.
        /// </summary>
        internal static void LoadModDecalRegistry(ModAsset decalRegistry) {
            Logger.Log(LogLevel.Debug, "Decal Registry", $"Loading registry for {decalRegistry.Source.Name}");

            string basePath = ((patch_Atlas) GFX.Game).RelativeDataPath + "decals/";
            List<string> localDecals = decalRegistry.Source.Map.Keys
                .Where(s => s.StartsWith(basePath, StringComparison.Ordinal))
                .Select(s => s.AsSpan(basePath.Length).TrimEnd("0123456789").ToString().ToLower())
                .Distinct()
                .ToList();

            foreach (KeyValuePair<string, DecalInfo> decalRegistration in ReadDecalRegistryXml(decalRegistry)) {
                string registeredPath = decalRegistration.Key;
                DecalInfo info = decalRegistration.Value;

                bool found = false;
                if (registeredPath.EndsWith("*", StringComparison.Ordinal) || registeredPath.EndsWith("/", StringComparison.Ordinal)) {
                    var registeredPathTrimmed = registeredPath.AsSpan().TrimEnd('*');
                    foreach (string decalPathStr in localDecals) {
                        var decalPath = decalPathStr.AsSpan();
                        // Wildcard matches must be longer than the subpath, and don't match on decals in subfolders
                        if (decalPath.Length <= registeredPathTrimmed.Length) {
                            continue;
                        }

                        if (!decalPath.StartsWith(registeredPathTrimmed)) {
                            continue;
                        }
                        
                        if (decalPath.LastIndexOf('/') <= registeredPathTrimmed.Length - 1) {
                            RegisterDecal(decalPathStr, info);
                            found = true;
                        }
                    }
                } else if (localDecals.Contains(registeredPath)) {
                    RegisterDecal(registeredPath, info);
                    found = true;
                }

                if (!found) {
                    Logger.Log(LogLevel.Warn, "Decal Registry", $"Could not find any decals in {decalRegistry.Source.Name} under path {decalRegistration.Key}");
                }
            }
        }

        /// <summary>
        /// Reads a decal registry file and returns a sorted list of properties per decal path.
        /// </summary>
        private static List<KeyValuePair<string, DecalInfo>> ReadDecalRegistryXml(ModAsset decalRegistry) {
            XmlDocument doc = new XmlDocument();
            doc.Load(decalRegistry.Stream);

            List<KeyValuePair<string, DecalInfo>> elements = new();

            XmlElement decalElement = doc["decals"];
            if (decalElement == null) {
                Logger.Log(LogLevel.Warn, "Decal Registry", $"Could not parse the 'decals' tag for the {decalRegistry.Source.Name} registry.");
                return elements;
            }

            foreach (XmlNode node in decalElement) {
                if (node is XmlElement decal) {
                    string decalPath = decal.Attr("path", null)?.ToLower();
                    if (decalPath == null) {
                        Logger.Log(LogLevel.Warn, "Decal Registry", $"Decal in the {decalRegistry.Source.Name} registry is missing a 'path' attribute!");
                        continue;
                    }

                    DecalInfo info = new DecalInfo {
                        CustomProperties = new List<KeyValuePair<string, XmlAttributeCollection>>()
                    };

                    foreach (XmlNode node2 in decal.ChildNodes) {
                        if (node2 is XmlElement property) {
                            info.CustomProperties.Add(new KeyValuePair<string, XmlAttributeCollection>(property.Name, property.Attributes));
                        }
                    }

                    elements.Add(new KeyValuePair<string, DecalInfo>(decalPath, info));
                }
            }

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

            return elements;
        }

        private static void RegisterDecal(string decalPath, DecalInfo info) {
            if (info.Handlers is null) {
                info.Handlers = new(info.CustomProperties.Count);
                
                // Apply "scale" first since it affects other properties.
                if (info.CustomProperties.Find(p => p.Key == "scale") is { Value: {} } scaleProp) {
                    if (CreateHandlerOrNull(decalPath, scaleProp.Key, scaleProp.Value) is { } handler)
                        info.Handlers.Add(handler);
                }
                
                foreach ((string propertyName, var xml) in info.CustomProperties) {
                    if (propertyName != "scale" && CreateHandlerOrNull(decalPath, propertyName, xml) is { } handler)
                        info.Handlers.Add(handler);
                }
            }
            
            if (RegisteredDecals.ContainsKey(decalPath)) {
                Logger.Log(LogLevel.Verbose, "Decal Registry", $"Replaced decal {decalPath}");
            } else {
                Logger.Log(LogLevel.Verbose, "Decal Registry", $"Registered decal {decalPath}");
            }
            RegisteredDecals[decalPath] = info;
        }

        public struct DecalInfo {
            /// <summary>
            /// PropertyName -> AttributeCollection
            /// </summary>
            public List<KeyValuePair<string, XmlAttributeCollection>> CustomProperties;

            internal List<DecalRegistryHandler> Handlers { get; set; }
        }
    }
}
