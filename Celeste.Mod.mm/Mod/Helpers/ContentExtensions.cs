using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
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

        public static bool IsSubtexture(this MTexture input) =>
            input.ClipRect.X != 0 ||
            input.ClipRect.Y != 0 ||
            input.ClipRect.Width != input.Texture.Texture.Width ||
            input.ClipRect.Height != input.Texture.Texture.Height;

        public static Texture2D GetSubtextureCopy(this MTexture input) {
            if (input == null)
                return null;
            // TODO: Non-copy-to-CPU codepath!

            Color[] dataInput = new Color[input.Texture.Texture.Width * input.Texture.Texture.Height];
            input.Texture.Texture.GetData(dataInput);
            Color[] dataOutput = new Color[input.ClipRect.Width * input.ClipRect.Height];

            for (int y = 0; y < input.ClipRect.Height; y++)
                for (int x = 0; x < input.ClipRect.Width; x++)
                    dataOutput[y * input.ClipRect.Width + x] = dataInput[(input.ClipRect.Y + y) * input.Texture.Texture.Width + input.ClipRect.X + x];

            Texture2D output = new Texture2D(Celeste.Instance.GraphicsDevice, input.ClipRect.Width, input.ClipRect.Height);
            output.SetData(dataOutput);
            return output;
        }

        public static Texture2D Premultiply(this Texture2D texture) {
            if (texture == null)
                return null;
            // TODO: Non-copy-to-CPU codepath!

            Color[] data = new Color[texture.Width * texture.Height];
            texture.GetData(data);

            for (int i = 0; i < data.Length; i++) {
                Color c = data[i];
                if (c.A == 0 || c.A == 255)
                    // Skip mul by 0 or 1
                    continue;
                c = new Color(
                    (int) Math.Round(c.R * c.A / 255D),
                    (int) Math.Round(c.G * c.A / 255D),
                    (int) Math.Round(c.B * c.A / 255D),
                    c.A
                );
                data[i] = c;
            }

            texture.SetData(data);
            return texture;
        }

        public static Texture2D Postdivide(this Texture2D texture) {
            if (texture == null)
                return null;
            // TODO: Non-copy-to-CPU codepath!

            Color[] data = new Color[texture.Width * texture.Height];
            texture.GetData(data);

            for (int i = 0; i < data.Length; i++) {
                Color c = data[i];
                if (c.A == 0 || c.A == 255)
                    // Skip div by 0 or 1
                    continue;
                float a = c.A;
                c = new Color(
                    (int) Math.Round(255D * c.R / a),
                    (int) Math.Round(255D * c.G / a),
                    (int) Math.Round(255D * c.B / a),
                    c.A
                );
                data[i] = c;
            }

            texture.SetData(data);
            return texture;
        }

    }
}
