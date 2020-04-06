#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    static class patch_Fonts {

        public static extern PixelFont orig_Load(string face);
        public static PixelFont Load(string face) {
            PixelFont font = orig_Load(face);
            Emoji.Fill(font);
            return font;
        }

        // don't change anything in this method, except for also listing custom fonts from mods
        [MonoModIgnore]
        [ProxyFileCalls]
        [PatchFontsPrepare]
        public static extern void Prepare();

        private static string[] _GetFiles(string path, string searchPattern, SearchOption searchOption) {
            string[] vanillaFiles = Directory.GetFiles(path, searchPattern, searchOption);

            lock (Everest.Content.Map)
                return Everest.Content.Map.Values
                    .Where(asset => asset.Type == typeof(AssetTypeFont))
                    .Select(asset => Path.Combine(Engine.ContentDirectory, asset.PathVirtual + "." + asset.Format).Replace('/', Path.DirectorySeparatorChar))
                    .Union(vanillaFiles)
                    .ToArray();
        }
    }
}
