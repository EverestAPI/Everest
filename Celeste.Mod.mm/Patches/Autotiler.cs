#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_Autotiler : Autotiler {

        public patch_Autotiler(string filename)
            : base(filename) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private extern void orig_ReadInto(patch_TerrainType data, Tileset tileset, XmlElement xml);
        private void ReadInto(patch_TerrainType data, Tileset tileset, XmlElement xml) {
            orig_ReadInto(data, tileset, xml);

            if (xml.HasAttr("sound"))
                SurfaceIndex.TileToIndex[xml.AttrChar("id")] = xml.AttrInt("sound");
        }

        // Required because TerrainType is private.
        [MonoModIgnore]
        private class patch_TerrainType {
        }

    }
}
