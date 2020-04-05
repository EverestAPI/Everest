using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Utils;
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
using SD = System.Drawing;
using SDI = System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Celeste.Mod {
    public static class ContentExtensions {

        /// <summary>
        /// Determine if the MTexture depicts a region of a larger VirtualTexture.
        /// </summary>
        /// <param name="input">The input texture.</param>
        /// <returns>True if the ClipRect is a subregion of the MTexture's VirtualTexture's Texture2D, false otherwise.</returns>
        public static bool IsSubtexture(this MTexture input) =>
            input.ClipRect.X != 0 ||
            input.ClipRect.Y != 0 ||
            input.ClipRect.Width != input.Texture.Texture.Width ||
            input.ClipRect.Height != input.Texture.Texture.Height;

        /// <summary>
        /// Create a new, standalone copy of the region accessed via the MTexture.
        /// </summary>
        /// <param name="input">The input texture.</param>
        /// <returns>The output texture, matching the input MTexture's ClipRect.</returns>
        public static Texture2D GetSubtextureCopy(this MTexture input) {
            if (input == null)
                return null;
            // TODO: Non-copy-to-CPU codepath!

            Color[] dataInput = new Color[input.Texture.Texture.Width * input.Texture.Texture.Height];
            input.Texture.Texture.GetData(dataInput);
            Color[] dataOutput = new Color[input.ClipRect.Width * input.ClipRect.Height];

            for (int y = input.ClipRect.Height - 1; y > -1; y--)
                for (int x = input.ClipRect.Width - 1; x > -1; x--)
                    dataOutput[y * input.ClipRect.Width + x] = dataInput[(input.ClipRect.Y + y) * input.Texture.Texture.Width + input.ClipRect.X + x];

            Texture2D output = new Texture2D(Celeste.Instance.GraphicsDevice, input.ClipRect.Width, input.ClipRect.Height);
            output.SetData(dataOutput);
            return output;
        }

        /// <summary>
        /// Create a new, standalone copy of the region accessed via the MTexture, with padding.
        /// </summary>
        /// <param name="input">The input texture.</param>
        /// <returns>The output texture, matching the input MTexture's Width and Height, with padding.</returns>
        public static Texture2D GetPaddedSubtextureCopy(this MTexture input) {
            if (input == null)
                return null;
            // TODO: Non-copy-to-CPU codepath!

            Color[] dataInput = new Color[input.Texture.Texture.Width * input.Texture.Texture.Height];
            input.Texture.Texture.GetData(dataInput);
            Color[] dataOutput = new Color[input.Width * input.Height];

            int xo = (int) Math.Round(input.DrawOffset.X);
            int yo = (int) Math.Round(input.DrawOffset.Y) * input.Width;

            for (int y = input.ClipRect.Height - 1; y > -1; y--)
                for (int x = input.ClipRect.Width - 1; x > -1; x--)
                    dataOutput[y * input.Width + x + yo + xo] = dataInput[(input.ClipRect.Y + y) * input.Texture.Texture.Width + input.ClipRect.X + x];

            Texture2D output = new Texture2D(Celeste.Instance.GraphicsDevice, input.Width, input.Height);
            output.SetData(dataOutput);
            return output;
        }

        /// <summary>
        /// Lazily late-premultiply a texture: Multiply the values of the R, G and B channels by the value of the A channel.
        /// </summary>
        /// <param name="texture">The input texture.</param>
        /// <returns>A premultiplied copy of the input texture.</returns>
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

        /// <summary>
        /// Lazily late-postdivide a texture: Divide the values of the R, G and B channels by the value of the A channel.
        /// </summary>
        /// <param name="texture">The input texture.</param>
        /// <returns>A postdivided copy of the input texture.</returns>
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

        /// <summary>
        /// Load a texture and lazily late-premultiply it: Multiply the values of the R, G and B channels by the value of the A channel.
        /// </summary>
        public static Texture2D LoadTextureLazyPremultiply(GraphicsDevice gd, Stream stream) {
            _LoadTextureLazyPremultiply(gd, stream, out int w, out int h, out byte[] data);
            Texture2D tex = new Texture2D(gd, w, h, false, SurfaceFormat.Color);
            tex.SetData(data);
            return tex;
        }

        public static void LoadTextureLazyPremultiply(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data) {
            _LoadTextureLazyPremultiply(gd, stream, out w, out h, out data);
        }

        [MonoModIgnore]
        private static extern void _LoadTextureLazyPremultiply(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data);

        [MonoModIfFlag("XNA")]
        [MonoModPatch("_LoadTextureLazyPremultiply")]
        [MonoModReplace]
        private static void _LoadTextureLazyPremultiplyXNA(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data) {
            using (SD.Bitmap bmp = new SD.Bitmap(stream)) {
                w = bmp.Width;
                h = bmp.Height;
                int depth = SD.Image.GetPixelFormatSize(bmp.PixelFormat);

                SD.Bitmap copy = null;
                if (depth != 32)
                    copy = bmp.Clone(new SD.Rectangle(0, 0, w, h), SDI.PixelFormat.Format32bppArgb);
                using (copy) {

                    SD.Bitmap src = copy ?? bmp;

                    SDI.BitmapData srcData = src.LockBits(
                        new SD.Rectangle(0, 0, w, h),
                        SDI.ImageLockMode.ReadOnly,
                        src.PixelFormat
                    );

                    data = new byte[w * h * 4];

                    unsafe {
                        byte* from = (byte*) srcData.Scan0;
                        fixed (byte* to = data) {
                            for (int i = data.Length - 1 - 3; i > -1; i -= 4) {
                                byte r = from[i + 2];
                                byte g = from[i + 1];
                                byte b = from[i + 0];
                                byte a = from[i + 3];

                                if (a == 0)
                                    continue;

                                if (a == 255) {
                                    to[i + 0] = r;
                                    to[i + 1] = g;
                                    to[i + 2] = b;
                                    to[i + 3] = a;
                                    continue;
                                }

                                to[i + 0] = (byte) Math.Round(r * a / 255D);
                                to[i + 1] = (byte) Math.Round(g * a / 255D);
                                to[i + 2] = (byte) Math.Round(b * a / 255D);
                                to[i + 3] = a;
                            }
                        }
                    }

                    src.UnlockBits(srcData);
                }
            }
        }

        [MonoModIfFlag("FNA")]
        [MonoModPatch("_LoadTextureLazyPremultiply")]
        [MonoModReplace]
        private static void _LoadTextureLazyPremultiplyFNA(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data) {
            Texture2D.TextureDataFromStreamEXT(stream, out w, out h, out data);
            unsafe {
                fixed (byte* raw = data) {
                    for (int i = data.Length - 1 - 3; i > -1; i -= 4) {
                        byte a = raw[i + 3];

                        if (a == 0 || a == 255)
                            continue;

                        raw[i + 0] = (byte) Math.Round(raw[i + 0] * a / 255D);
                        raw[i + 1] = (byte) Math.Round(raw[i + 1] * a / 255D);
                        raw[i + 2] = (byte) Math.Round(raw[i + 2] * a / 255D);
                        raw[i + 3] = a;
                    }
                }
            }
        }


    }
}
