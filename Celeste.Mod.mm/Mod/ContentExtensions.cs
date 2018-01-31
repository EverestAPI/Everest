using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using MonoMod;
using MonoMod.Helpers;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class ContentExtensions {

        public static Texture2D Patch(this Texture2D texture, List<AssetMetadata> patches) {
            if (texture == null)
                return null;
            if (patches == null || patches.Count == 0)
                return texture;

            // TODO: Overlay the patches over the input texture.

            return texture;
        }

    }
}
