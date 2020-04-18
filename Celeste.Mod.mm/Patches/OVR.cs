#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OVR {

        public static extern void orig_Load();
        public static void Load() {
            orig_Load();

            // THe vanilla overlay texture has got a 4x4 transparent blob formed by transparent pixels at each corner.
            MTexture overlay = OVR.Atlas["overlay"];
            if (overlay.Texture.GetMetadata() == null) {
                Texture2D texture = overlay.Texture.Texture;
                if (overlay.ClipRect.X == 0 && overlay.ClipRect.Y == 0 && overlay.ClipRect.Width == texture.Width && overlay.ClipRect.Height == texture.Height) {
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
        }

    }
}
