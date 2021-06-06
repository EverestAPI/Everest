#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Monocle;
using MonoMod;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Celeste.Pico8 {
    class patch_Emulator : Emulator {

        // We're effectively in Emulator, but still need to "expose" private fields to our mod.
        private byte[] tilemap;

        public patch_Emulator(Scene returnTo, int levelX = 0, int levelY = 0)
            : base(returnTo, levelX, levelY) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(Scene returnTo, int levelX = 0, int levelY = 0);
        [MonoModConstructor]
        public void ctor(Scene returnTo, int levelX = 0, int levelY = 0) {
            orig_ctor(returnTo, levelX, levelY);

            // If a custom tilemap is present, reparse it.
            if (Everest.Content.TryGet<AssetTypeText>("Pico8Tilemap", out ModAsset asset)) {
                string text;
                using (StreamReader reader = new StreamReader(asset.Stream))
                    text = reader.ReadToEnd();
                text = Regex.Replace(text, "\\s+", "");
                tilemap = new byte[text.Length / 2];
                int length = text.Length;
                int half = length / 2;
                for (int i = 0; i < length; i += 2) {
                    char c1 = text[i];
                    char c2 = text[i + 1];
                    tilemap[i / 2] = (byte) int.Parse(
                        (i < half) ? (c1.ToString() + c2.ToString()) : (c2.ToString() + c1.ToString()),
                        NumberStyles.HexNumber
                    );
                }
            }
        }

    }
}
