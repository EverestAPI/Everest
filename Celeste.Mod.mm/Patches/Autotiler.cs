#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Monocle;
using System.Collections.Generic;
using System.Xml;

namespace Celeste {
    class patch_Autotiler : Autotiler {

        private Dictionary<char, patch_TerrainType> lookup;

        public patch_Autotiler(string filename)
            : base(filename) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private extern void orig_ReadInto(patch_TerrainType data, Tileset tileset, XmlElement xml);
        private void ReadInto(patch_TerrainType data, Tileset tileset, XmlElement xml) {
            orig_ReadInto(data, tileset, xml);

            if (xml.HasAttr("sound"))
                SurfaceIndex.TileToIndex[xml.AttrChar("id")] = xml.AttrInt("sound");

            if (xml.HasAttr("debris"))
                data.Debris = xml.Attr("debris");
        }

        public bool TryGetCustomDebris(out string path, char tiletype) {
            return !string.IsNullOrEmpty(path = lookup[tiletype].Debris);
        }

        // Required because TerrainType is private.
        private class patch_TerrainType {
            public string Debris;
        }

    }
}
