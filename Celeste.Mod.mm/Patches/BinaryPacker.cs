#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using MonoMod;
using System.Collections.Generic;
using System.IO;
using System;
using System.Globalization;

namespace Celeste {
    static class patch_BinaryPacker {
        [MonoModIgnore]
        private static string[] stringLookup;

        [MonoModIgnore] // We don't want to change anything about the method...
        [ProxyFileCalls] // ... except for proxying all System.IO.File.* calls to Celeste.Mod.FileProxy.*
        public static extern BinaryPacker.Element FromBinary(string filename);

        // optimise the method
        [MonoModReplace]
        private static BinaryPacker.Element ReadElement(BinaryReader reader) {
            BinaryPacker.Element element = new();
            element.Name = stringLookup[reader.ReadInt16()];
            
            byte attributeCount = reader.ReadByte();
            if (attributeCount > 0)
                element.Attributes = new(attributeCount); // insert attribute count here to reduce memory allocations in the future

            for (int i = 0; i < attributeCount; i++) {
                string key = stringLookup[reader.ReadInt16()];
                byte type = reader.ReadByte();

                object obj = type switch {
                    0 => reader.ReadBoolean(),
                    1 => Convert.ToInt32(reader.ReadByte()),
                    2 => Convert.ToInt32(reader.ReadInt16()),
                    3 => reader.ReadInt32(),
                    4 => reader.ReadSingle(),
                    5 => stringLookup[reader.ReadInt16()],
                    6 => reader.ReadString(),
                    7 => ReadRunLengthEncoded(reader),
                    _ => null
                };
                element.Attributes.Add(key, obj);
            }

            short childCount = reader.ReadInt16();
            if (childCount > 0)
                element.Children = new(childCount); // provide the starting capacity here
            for (int i = 0; i < childCount; i++)
                element.Children.Add(ReadElement(reader));

            return element;
        }

        private static string ReadRunLengthEncoded(BinaryReader reader) {
            short count = reader.ReadInt16();
            
            byte[] buffer = _runLengthEncodedBuffer ??= new byte[short.MaxValue];
            int read = reader.Read(buffer, 0, count);

            return patch_RunLengthEncoding.Decode(buffer.AsSpan()[..read]);
        }

        [ThreadStatic]
        private static byte[] _runLengthEncodedBuffer;

        public class Element : BinaryPacker.Element {
            public extern bool orig_HasAttr(string name);
            public new bool HasAttr(string name)
                => orig_HasAttr(name) || orig_HasAttr(name.ToLowerInvariant());

            [MonoModReplace] // implement case-insensitivity
            public new string Attr(string name, string defaultValue = "") {
                if (!AttrCore(name, out object stored))
                    return defaultValue;

                return stored.ToString();
            }

            [MonoModReplace] // implement case-insensitivity
            public new bool AttrBool(string name, bool defaultValue = false) {
                if (!AttrCore(name, out object stored))
                    return defaultValue;
                
                return stored is bool flag ? flag : Convert.ToBoolean(stored, CultureInfo.InvariantCulture);
            }

            [MonoModReplace] // implement case-insensitivity
            public new float AttrFloat(string name, float defaultValue = 0f){
                if (!AttrCore(name, out object stored))
                    return defaultValue;
                
                return stored is float f ? f : Convert.ToSingle(stored, CultureInfo.InvariantCulture);
            }

            /// <summary>
            /// Core method for all Attr methods, which handles case-insensitive fallbacks.
            /// </summary>
            /// <returns>Whether a given attribute exists in the element</returns>
            private bool AttrCore(string name, out object stored) {
                if (Attributes is not { } attributes) {
                    stored = null;
                    return false;
                }

                if (attributes.TryGetValue(name, out stored)) {
                    return true;
                }
                
                if (attributes.TryGetValue(name.ToLowerInvariant(), out stored)) {
                    return true;
                }

                stored = null;
                return false;
            }
        }

    }
}
