#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_HiresSnow : HiresSnow {

        public patch_HiresSnow(float overlayAlpha = 0.45f)
            : base(overlayAlpha) {
            // no-op.
        }

        public extern void orig_ctor(float overlayAlpha = 0.45f);
        [MonoModConstructor]
        public void ctor(float overlayAlpha = 0.45f) {
            // THe vanilla overlay texture has got a 4x4 transparent blob formed by transparent pixels at each corner.
            MTexture overlay = OVR.Atlas["overlay"];
            if (((patch_VirtualTexture) (object) overlay.Texture).Metadata == null) {
                Texture2D texture = overlay.Texture.Texture;
                if (overlay.ClipRect.X == 0 && overlay.ClipRect.Y == 0 &&
                    overlay.ClipRect.Width == texture.Width && overlay.ClipRect.Height == texture.Height) {
                    Color[] data = new Color[texture.Width * texture.Height];
                    texture.GetData(data);

                    bool changed = false;

                    Color c = data[0];
                    if (c.A == 0 || (c.R == 0 && c.G == 0 && c.B == 0)) {
                        data[0] = new Color(65, 116, 225, 255);
                        changed = true;
                    }

                    c = data[texture.Width - 1];
                    if (c.A == 0 || (c.R == 0 && c.G == 0 && c.B == 0)) {
                        data[texture.Width - 1] = new Color(67, 117, 223, 255);
                        changed = true;
                    }

                    c = data[texture.Width * (texture.Height - 1)];
                    if (c.A == 0 || (c.R == 0 && c.G == 0 && c.B == 0)) {
                        data[texture.Width * (texture.Height - 1)] = new Color(66, 118, 225, 255);
                        changed = true;
                    }

                    c = data[texture.Width * texture.Height - 1];
                    if (c.A == 0 || (c.R == 0 && c.G == 0 && c.B == 0)) {
                        data[texture.Width * texture.Height - 1] = new Color(64, 116, 223, 255);
                        changed = true;
                    }

                    if (changed) {
                        texture.SetData(data);
                    }
                }
            }

            orig_ctor(overlayAlpha);
        }

    }
}
