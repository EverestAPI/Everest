#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using System;
using Celeste.Mod;
using Monocle;
using MonoMod;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    static class patch_Fonts {
        // make vanilla private fields accessible to our patch.
        private static Dictionary<string, PixelFont> loadedFonts;

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

        [MonoModReplace] // this method is both unused and broken in vanilla.
        public static void Reload() {
            List<string> fontsToReload = new List<string>();
            foreach (string item in loadedFonts.Keys) {
                fontsToReload.Add(item);
            }
            foreach (string fontToReload in fontsToReload) {
                loadedFonts[fontToReload].Dispose();
                loadedFonts.Remove(fontToReload); // this line is missing from the vanilla method.
                Load(fontToReload);
            }
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the Fonts.Prepare method to also include custom fonts.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFontsPrepare))]
    class PatchFontsPrepareAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchFontsPrepare(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GetFiles = method.DeclaringType.FindMethod("System.String[] _GetFiles(System.String,System.String,System.IO.SearchOption)");

            bool found = false;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                if (instrs[instri].MatchCall("System.IO.Directory", "GetFiles")) {
                    instrs[instri].Operand = m_GetFiles;
                    found = true;
                }
            }

            if (!found) {
                throw new Exception("No call to Directory.GetFiles found in " + method.FullName + "!");
            }
        }

    }
}
