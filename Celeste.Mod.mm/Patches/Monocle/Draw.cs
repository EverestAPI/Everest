#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework.Graphics;

namespace Monocle {
    class patch_Draw {
        public static SpriteFont DefaultFont { get; private set; }

        internal extern static void orig_Initialize(GraphicsDevice graphicsDevice);

        internal static void Initialize(GraphicsDevice graphicsDevice) {
            orig_Initialize(graphicsDevice);

            // Fix the issue that the game crashes when the exception information printed on the console contains non-existent characters
            // If DefaultCharacter is null ArgumentException will be thrown.
            // The default character will be substituted if you draw or measure text that contains characters which were not included in the font
            DefaultFont.DefaultCharacter = '*';
        }
    }
}
