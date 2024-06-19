using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Globalization;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers {
    /// <summary>
    /// Represents a handler for a specific decal registry tag.
    /// A new instance of this class will be created for each xml entry.
    /// To register your handler, call <see cref="DecalRegistry.AddPropertyHandler{T}()"/>
    /// </summary>
    public abstract class DecalRegistryHandler {
        /// <summary>
        /// The name of this handler, which should match the xml tag name which creates this handler.
        /// This field will be accessed in <see cref="DecalRegistry.AddPropertyHandler{T}()"/>, as well as in error handling.
        /// </summary>
        public abstract string Name { get; }
        
        /// <summary>
        /// Parses the xml entry for an instance of this decal registry property.
        /// This function gets called once per class instance, while parsing the xml file.
        /// Use it to cache attributes from xml into fields on this handler instance.
        /// </summary>
        public abstract void Parse(XmlAttributeCollection xml);

        /// <summary>
        /// Applies this handler to the given decal.
        /// This function gets called for each decal, so make sure to do as much work as possible in <see cref="Parse"/> instead.
        /// </summary>
        public abstract void ApplyTo(Decal decal);
        
        /// <summary>
        /// Gets a parsable value from the xml attribute named <paramref name="attr"/>.
        /// If the attribute does not exist or is invalid, returns <paramref name="def"/>.
        /// </summary>
        public T Get<T>(XmlAttributeCollection xml, string attr, T def)
            where T : IParsable<T> {
            if (xml[attr] is not { } attribute) 
                return def;
            
            if (T.TryParse(attribute.Value, CultureInfo.InvariantCulture, out T parsed)) {
                return parsed;
            }
                
            Logger.Log(LogLevel.Warn, "Decal Registry", 
                $"Invalid '{typeof(T).Name}' value '{attribute.Value}' for attribute '{attr}' in property '{Name}'. Defaulting to {def}.");

            return def;
        }
        
        /// <summary>
        /// Gets a bool value from the xml attribute named <paramref name="attr"/>.
        /// If the attribute does not exist or is invalid, returns <paramref name="def"/>.
        /// </summary>
        // bool doesn't implement IParseable???
        public bool GetBool(XmlAttributeCollection xml, string attr, bool def) {
            if (xml[attr] is not { } attribute) 
                return def;
            
            if (bool.TryParse(attribute.Value, out bool parsed)) {
                return parsed;
            }
                
            Logger.Log(LogLevel.Warn, "Decal Registry", $"Invalid Bool value {attribute.Value} for attribute {attr} in property {Name}. Defaulting to {def}.");

            return def;
        }
        
        /// <summary>
        /// Gets a string value from the xml attribute named <paramref name="attr"/>.
        /// If the attribute does not exist or is invalid, returns <paramref name="def"/>.
        /// </summary>
        public string GetString(XmlAttributeCollection xml, string attr, string def) {
            if (xml[attr] is not { } attribute) 
                return def;

            return attribute.Value;
        }
        
        /// <summary>
        /// Gets a hex color value from the xml attribute named <paramref name="attr"/>, by calling <see cref="Calc.HexToColor(string)"/>.
        /// If the attribute does not exist or is invalid, returns <paramref name="def"/>.
        /// </summary>
        public Color GetHexColor(XmlAttributeCollection xml, string attr, Color def) {
            if (xml[attr] is not { } attribute) 
                return def;

            return Calc.HexToColor(attribute.Value);
        }

        /// <summary>
        /// Gets a CSV int array value from the xml attribute named <paramref name="attr"/>, by calling <see cref="Calc.ReadCSVIntWithTricks(string)"/>.
        /// If the attribute does not exist or is invalid, returns <paramref name="def"/>.
        /// </summary>
        public int[] GetCSVIntWithTricks(XmlAttributeCollection xml, string attr, string def) {
            if (xml[attr] is not { } attribute) 
                return Calc.ReadCSVIntWithTricks(def);

            try {
                return Calc.ReadCSVIntWithTricks(attribute.Value);
            } catch (Exception) {
                Logger.Log(LogLevel.Warn, "Decal Registry", $"Invalid CSVInt value {attribute.Value} for attribute {attr} in property {Name}. Defaulting to {def}.");
                return Calc.ReadCSVIntWithTricks(def);
            }
        }
        
        /// <summary>
        /// Equivalent to <see cref="Get{TParsable}"/>, but returns null for value types if the attribute does not exist.
        /// </summary>
        public TParsable? GetNullable<TParsable>(XmlAttributeCollection xml, string attr) where TParsable : struct, IParsable<TParsable> {
            if (xml[attr] is not { } attribute) 
                return null;
            
            if (TParsable.TryParse(attribute.Value, CultureInfo.InvariantCulture, out TParsable parsed)) {
                return parsed;
            }
                
            Logger.Log(LogLevel.Warn, "Decal Registry", $"Invalid {typeof(TParsable).Name} value {attribute.Value} for attribute {attr} in property {Name}. Defaulting to null.");

            return null;
        }
        
        public Vector2 GetVector2(XmlAttributeCollection xml, string attrX, string attrY, Vector2 def) {
            return new(Get(xml, attrX, def.X), Get(xml, attrY, def.Y));
        }
    }
}
    