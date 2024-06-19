using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.IO;
using System.Runtime.InteropServices;
using SD = System.Drawing;
using SDI = System.Drawing.Imaging;

namespace Celeste.Mod {
    public static class ContentExtensions {

        private delegate IntPtr d_FNA3D_ReadImageStream(Stream stream, out int width, out int height, out int len, int forceW = -1, int forceH = -1, bool zoom = false);
        private static d_FNA3D_ReadImageStream FNA3D_ReadImageStream =
            typeof(Game).Assembly
            .GetType("Microsoft.Xna.Framework.Graphics.FNA3D")
            ?.GetMethod("ReadImageStream")
            ?.CreateDelegate<d_FNA3D_ReadImageStream>();

        private delegate void d_FNA3D_Image_Free(IntPtr mem);
        private static d_FNA3D_Image_Free FNA3D_Image_Free =
            typeof(Game).Assembly
            .GetType("Microsoft.Xna.Framework.Graphics.FNA3D")
            ?.GetMethod("FNA3D_Image_Free")
            ?.CreateDelegate<d_FNA3D_Image_Free>();

        public static readonly bool TextureSetDataSupportsPtr = Everest.Flags.IsFNA;

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
        public static unsafe Texture2D GetSubtextureCopy(this MTexture input) {
            if (input == null)
                return null;
            // TODO: Non-copy-to-CPU codepath!

            Color[] dataInput = new Color[input.Texture.Texture.Width * input.Texture.Texture.Height];
            input.Texture.Texture.GetData(dataInput);
            Color[] dataOutput = new Color[input.ClipRect.Width * input.ClipRect.Height];

            int cx = input.ClipRect.X;
            int cy = input.ClipRect.Y;
            int cw = input.ClipRect.Width;
            int ch = input.ClipRect.Height;
            int tw = input.Texture.Texture.Width;

            fixed (Color* rawInput = dataInput)
            fixed (Color* rawOutput = dataOutput) {
                for (int y = ch - 1; y > -1; y--)
                    for (int x = cw - 1; x > -1; x--)
                        dataOutput[y * cw + x] = rawInput[(cy + y) * tw + cx + x];
            }

            Texture2D output = new Texture2D(Celeste.Instance.GraphicsDevice, input.ClipRect.Width, input.ClipRect.Height);
            output.SetData(dataOutput);
            return output;
        }

        /// <summary>
        /// Create a new, standalone copy of the region accessed via the MTexture, with padding.
        /// </summary>
        /// <param name="input">The input texture.</param>
        /// <returns>The output texture, matching the input MTexture's Width and Height, with padding.</returns>
        public static unsafe Texture2D GetPaddedSubtextureCopy(this MTexture input) {
            if (input == null)
                return null;
            // TODO: Non-copy-to-CPU codepath!

            Color[] dataInput = new Color[input.Texture.Texture.Width * input.Texture.Texture.Height];
            input.Texture.Texture.GetData(dataInput);
            Color[] dataOutput = new Color[input.Width * input.Height];

            int cx = input.ClipRect.X;
            int cy = input.ClipRect.Y;
            int cw = input.ClipRect.Width;
            int ch = input.ClipRect.Height;
            int tw = input.Texture.Texture.Width;
            int iw = input.Width;
            int offs = (int) Math.Round(input.DrawOffset.X) + (int) Math.Round(input.DrawOffset.Y) * input.Width;

            fixed (Color* rawInput = dataInput)
            fixed (Color* rawOutput = dataOutput) {
                for (int y = ch - 1; y > -1; y--)
                    for (int x = cw - 1; x > -1; x--)
                        rawOutput[y * iw + x + offs] = rawInput[(cy + y) * tw + cx + x];
            }

            Texture2D output = new Texture2D(Celeste.Instance.GraphicsDevice, input.Width, input.Height);
            output.SetData(dataOutput);
            return output;
        }

        /// <summary>
        /// Lazily late-premultiply a texture: Multiply the values of the R, G and B channels by the value of the A channel.
        /// </summary>
        /// <param name="texture">The input texture.</param>
        /// <returns>A premultiplied copy of the input texture.</returns>
        public static unsafe Texture2D Premultiply(this Texture2D texture) {
            if (texture == null)
                return null;
            // TODO: Non-copy-to-CPU codepath!

            Color[] data = new Color[texture.Width * texture.Height];
            texture.GetData(data);

            fixed (Color* raw = data) {
                for (int i = data.Length - 1; i > -1; i--) {
                    Color c = raw[i];
                    if (c.A == 0 || c.A == 255)
                        // Skip mul by 0 or 1
                        continue;
                    c = new Color(
                        (int) Math.Round(c.R * c.A / 255D),
                        (int) Math.Round(c.G * c.A / 255D),
                        (int) Math.Round(c.B * c.A / 255D),
                        c.A
                    );
                    raw[i] = c;
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// Lazily late-postdivide a texture: Divide the values of the R, G and B channels by the value of the A channel.
        /// </summary>
        /// <param name="texture">The input texture.</param>
        /// <returns>A postdivided copy of the input texture.</returns>
        public static unsafe Texture2D Postdivide(this Texture2D texture) {
            if (texture == null)
                return null;
            // TODO: Non-copy-to-CPU codepath!

            Color[] data = new Color[texture.Width * texture.Height];
            texture.GetData(data);

            fixed (Color* raw = data) {
                for (int i = data.Length - 1; i > -1; i--) {
                    Color c = raw[i];
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
                    raw[i] = c;
                }
            }

            texture.SetData(data);
            return texture;
        }

        public static void SetData(this Texture2D tex, IntPtr ptr) {
            _SetTextureDataPtr(tex, ptr);
        }

        public static void LoadTextureRaw(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data) {
            _LoadTextureRaw(gd, stream, out w, out h, out data, out _, true);
        }

        public static void LoadTextureRaw(GraphicsDevice gd, Stream stream, out int w, out int h, out IntPtr data) {
            _LoadTextureRaw(gd, stream, out w, out h, out _, out data, false);
        }

        /// <summary>
        /// Load a texture and lazily late-premultiply it: Multiply the values of the R, G and B channels by the value of the A channel.
        /// </summary>
        public static Texture2D LoadTextureLazyPremultiply(GraphicsDevice gd, Stream stream) {
            return _LoadTextureLazyPremultiplyFull(gd, stream);
        }

        public static void LoadTextureLazyPremultiply(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data) {
            _LoadTextureLazyPremultiply(gd, stream, out w, out h, out data, out _, true);
        }

        public static void LoadTextureLazyPremultiply(GraphicsDevice gd, Stream stream, out int w, out int h, out IntPtr data) {
            _LoadTextureLazyPremultiply(gd, stream, out w, out h, out _, out data,false);
        }

        public static void UnloadTextureRaw(IntPtr dataPtr) {
            _UnloadTextureRaw(dataPtr);
        }

        [MonoModIgnore]
        private static extern void _SetTextureDataPtr(Texture2D tex, IntPtr ptr);

        [MonoModIgnore]
        private static extern Texture2D _LoadTextureLazyPremultiplyFull(GraphicsDevice gd, Stream stream);

        [MonoModIgnore]
        private static extern void _LoadTextureRaw(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data, out IntPtr dataPtr, bool gc);

        [MonoModIgnore]
        private static extern void _LoadTextureLazyPremultiply(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data, out IntPtr dataPtr, bool gc);

        [MonoModIgnore]
        private static extern void _UnloadTextureRaw(IntPtr dataPtr);

        [MonoModIfFlag("XNA")]
        [MonoModPatch("_SetTextureDataPtr")]
        [MonoModReplace]
        private static unsafe void _SetTextureDataPtrXNA(Texture2D tex, IntPtr ptr) {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException();

            byte[] copy = new byte[tex.Width * tex.Height * 4];
            Marshal.Copy(ptr, copy, 0, copy.Length);
            tex.SetData(copy);
        }

        [MonoModIfFlag("XNA")]
        [MonoModPatch("_LoadTextureLazyPremultiplyFull")]
        [MonoModReplace]
        private static unsafe Texture2D _LoadTextureLazyPremultiplyFullXNA(GraphicsDevice gd, Stream stream) {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException();

            _LoadTextureLazyPremultiply(gd, stream, out int w, out int h, out byte[] data, out _, true);
            Texture2D tex = new Texture2D(gd, w, h, false, SurfaceFormat.Color);
            tex.SetData(data);
            return tex;
        }

        [MonoModIfFlag("XNA")]
        [MonoModPatch("_LoadTextureRaw")]
        [MonoModReplace]
        private static unsafe void _LoadTextureRawXNA(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data, out IntPtr dataPtr, bool gc) {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException();

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

                    int length = w * h * 4;
                    if (gc) {
                        data = new byte[length];
                        dataPtr = IntPtr.Zero;
                    } else {
                        data = Array.Empty<byte>();
                        dataPtr = Marshal.AllocHGlobal(length);
                    }

                    byte* from = (byte*) srcData.Scan0;
                    fixed (byte* dataPin = data) {
                        byte* to = gc ? dataPin : (byte*) dataPtr;
                        for (int i = length - 1 - 3; i > -1; i -= 4) {
                            to[i + 0] = from[i + 2];
                            to[i + 1] = from[i + 1];
                            to[i + 2] = from[i + 0];
                            to[i + 3] = from[i + 3];
                        }
                    }

                    src.UnlockBits(srcData);
                }
            }
        }

        [MonoModIfFlag("XNA")]
        [MonoModPatch("_LoadTextureLazyPremultiply")]
        [MonoModReplace]
        private static unsafe void _LoadTextureLazyPremultiplyXNA(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data, out IntPtr dataPtr, bool gc) {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException();

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

                    int length = w * h * 4;
                    if (gc) {
                        data = new byte[length];
                        dataPtr = IntPtr.Zero;
                    } else {
                        data = Array.Empty<byte>();
                        dataPtr = Marshal.AllocHGlobal(length);
                    }

                    byte* from = (byte*) srcData.Scan0;
                    fixed (byte* dataPin = data) {
                        byte* to = gc ? dataPin : (byte*) dataPtr;
                        for (int i = length - 1 - 3; i > -1; i -= 4) {
                            byte r = from[i + 2];
                            byte g = from[i + 1];
                            byte b = from[i + 0];
                            byte a = from[i + 3];

                            if (a == 0) {
                                to[i + 0] = 0;
                                to[i + 1] = 0;
                                to[i + 2] = 0;
                                to[i + 3] = 0;
                                continue;
                            }

                            if (a == 255) {
                                to[i + 0] = r;
                                to[i + 1] = g;
                                to[i + 2] = b;
                                to[i + 3] = a;
                                continue;
                            }

                            to[i + 0] = (byte) (r * a / 255D);
                            to[i + 1] = (byte) (g * a / 255D);
                            to[i + 2] = (byte) (b * a / 255D);
                            to[i + 3] = a;
                        }
                    }

                    src.UnlockBits(srcData);
                }
            }
        }

        [MonoModIfFlag("XNA")]
        [MonoModPatch("_UnloadTextureRaw")]
        [MonoModReplace]
        private static unsafe void _UnloadTextureRawXNA(IntPtr dataPtr) {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException();

            Marshal.FreeHGlobal(dataPtr);
        }

        [MonoModIfFlag("FNA")]
        [MonoModPatch("_SetTextureDataPtr")]
        [MonoModReplace]
        private static unsafe void _SetTextureDataPtrFNA(Texture2D tex, IntPtr ptr) {
            tex.SetDataPointerEXT(0, null, ptr, tex.Width * tex.Height * 4);
        }

        [MonoModIfFlag("FNA")]
        [MonoModPatch("_LoadTextureLazyPremultiplyFull")]
        [MonoModReplace]
        private static unsafe Texture2D _LoadTextureLazyPremultiplyFullFNA(GraphicsDevice gd, Stream stream) {
            _LoadTextureLazyPremultiply(gd, stream, out int w, out int h, out _, out IntPtr dataPtr, false);
            Texture2D tex = new Texture2D(gd, w, h, false, SurfaceFormat.Color);
            tex.SetDataPointerEXT(0, null, dataPtr, w * h * 4);
            _UnloadTextureRaw(dataPtr);
            return tex;
        }

        [MonoModIfFlag("FNA")]
        [MonoModPatch("_LoadTextureRaw")]
        [MonoModReplace]
        private static unsafe void _LoadTextureRawFNA(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data, out IntPtr dataPtr, bool gc) {
            if (gc) {
                Texture2D.TextureDataFromStreamEXT(stream, out w, out h, out data);
                dataPtr = IntPtr.Zero;
            } else {
                data = Array.Empty<byte>();
                dataPtr = FNA3D_ReadImageStream(stream, out w, out h, out _);
            }
        }


        [MonoModIfFlag("FNA")]
        [MonoModPatch("_LoadTextureLazyPremultiply")]
        [MonoModReplace]
        private static unsafe void _LoadTextureLazyPremultiplyFNA(GraphicsDevice gd, Stream stream, out int w, out int h, out byte[] data, out IntPtr dataPtr, bool gc) {
            int length;
            if (gc) {
                Texture2D.TextureDataFromStreamEXT(stream, out w, out h, out data);
                dataPtr = IntPtr.Zero;
                length = data.Length;
            } else {
                data = Array.Empty<byte>();
                dataPtr = FNA3D_ReadImageStream(stream, out w, out h, out length);
            }

            fixed (byte* dataPin = data) {
                byte* raw = gc ? dataPin : (byte*) dataPtr;
                for (int i = length - 1 - 3; i > -1; i -= 4) {
                    byte a = raw[i + 3];

                    if (a is 0 or 255)
                        continue;

                    double by = a / 255D;
                    raw[i + 0] = (byte) (raw[i + 0] * by);
                    raw[i + 1] = (byte) (raw[i + 1] * by);
                    raw[i + 2] = (byte) (raw[i + 2] * by);
                    // raw[i + 3] = a;
                }
            }
        }

        [MonoModIfFlag("FNA")]
        [MonoModPatch("_UnloadTextureRaw")]
        [MonoModReplace]
        private static unsafe void _UnloadTextureRawFNA(IntPtr dataPtr) {
            FNA3D_Image_Free(dataPtr);
        }

    }
}
