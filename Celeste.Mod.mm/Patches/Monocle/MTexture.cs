#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Monocle {
    class patch_MTexture : MTexture {

        // We're effectively in MTexture, but still need to "expose" private fields to our mod.
        public extern VirtualTexture orig_get_Texture();
        public extern void orig_set_Texture(VirtualTexture value);
        public new VirtualTexture Texture {
            get {
                return OverlayTexture?.Texture ?? orig_get_Texture();
            }
            set {
                if (OverlayTexture != null)
                    OverlayTexture.Texture = value;
                else
                    orig_set_Texture(value);
            }
        }
        public extern Rectangle orig_get_ClipRect();
        public extern void orig_set_ClipRect(Rectangle value);
        public new Rectangle ClipRect {
            get {
                return OverlayTexture?.ClipRect ?? orig_get_ClipRect();
            }
            set {
                if (OverlayTexture != null)
                    OverlayTexture.ClipRect = value;
                else
                    orig_set_ClipRect(value);
            }
        }
        public extern string orig_get_AtlasPath();
        public extern void orig_set_AtlasPath(string value);
        public new string AtlasPath {
            get {
                return OverlayMeta?.AtlasPath ?? orig_get_AtlasPath();
            }
            set {
                if (OverlayMeta != null)
                    OverlayMeta.AtlasPath = value;
                else
                    orig_set_AtlasPath(value);
            }
        }
        public extern Vector2 orig_get_DrawOffset();
        public extern void orig_set_DrawOffset(Vector2 value);
        public new Vector2 DrawOffset {
            get {
                return OverlayMeta?.DrawOffset ?? orig_get_DrawOffset();
            }
            set {
                if (OverlayMeta != null)
                    OverlayMeta.DrawOffset = value;
                else
                    orig_set_DrawOffset(value);
            }
        }
        public extern int orig_get_Width();
        public extern void orig_set_Width(int value);
        public new int Width {
            get {
                return OverlayMeta?.Width ?? orig_get_Width();
            }
            set {
                if (OverlayMeta != null)
                    OverlayMeta.Width = value;
                else
                    orig_set_Width(value);
            }
        }
        public extern int orig_get_Height();
        public extern void orig_set_Height(int value);
        public new int Height {
            get {
                return OverlayMeta?.Height ?? orig_get_Height();
            }
            set {
                if (OverlayMeta != null)
                    OverlayMeta.Height = value;
                else
                    orig_set_Height(value);
            }
        }

        public new Vector2 Center => new Vector2(Width * 0.5f, Height * 0.5f);
        public new float LeftUV => ClipRect.Left / (float) Texture.Width;
        public new float RightUV => ClipRect.Right / (float) Texture.Width;
        public new float TopUV => ClipRect.Top / (float) Texture.Height;
        public new float BottomUV => ClipRect.Bottom / (float) Texture.Height;

        public List<MTextureOverlay> Overlays;
        public MTextureOverlay OverlayTexture {
            get {
                if (Overlays == null || Overlays.Count == 0)
                    return null;
                for (int i = Overlays.Count - 1; i > -1; --i) {
                    MTextureOverlay overlay = Overlays[i];
                    if (overlay.IsActiveTexture)
                        return overlay;
                }
                return null;
            }
        }
        public MTextureOverlay OverlayMeta {
            get {
                if (Overlays == null || Overlays.Count == 0)
                    return null;
                for (int i = Overlays.Count - 1; i > -1; --i) {
                    MTextureOverlay overlay = Overlays[i];
                    if (overlay.IsActiveMeta)
                        return overlay;
                }
                return null;
            }
        }

        // Patching constructors is ugly.
        public extern void orig_ctor_MTexture(MTexture parent, int x, int y, int width, int height);
        [MonoModConstructor]
        public void ctor_MTexture(MTexture parent, int x, int y, int width, int height) {
            orig_ctor_MTexture(parent, x, y, width, height);
            AddOverlay(new MTextureParent(parent).GetRelativeRect(x, y, width, height));
            AddOverlay(new MTextureOverlay {
                // ClipRect = parent.GetRelativeRect(x, y, width, height),
                DrawOffset = new Vector2(-Math.Min(x - parent.DrawOffset.X, 0f), -Math.Min(y - parent.DrawOffset.Y, 0f)),
                Width = width,
                Height = height,
            });
        }

        public extern void orig_ctor_MTexture(MTexture parent, string atlasPath, Rectangle clipRect, Vector2 drawOffset, int width, int height);
        [MonoModConstructor]
        public void ctor_MTexture(MTexture parent, string atlasPath, Rectangle clipRect, Vector2 drawOffset, int width, int height) {
            orig_ctor_MTexture(parent, atlasPath, clipRect, drawOffset, width, height);
            AddOverlay(new MTextureParent(parent).GetRelativeRect(clipRect));
            AddOverlay(new MTextureOverlay {
                AtlasPath = atlasPath,
                // ClipRect = parent.GetRelativeRect(clipRect),
                DrawOffset = drawOffset,
                Width = width,
                Height = height,
            });
        }

        [MonoModReplace]
        private void SetUtil() {
            // no-op - we dynamically calculate Center and the UVs now.
        }

        public void SetAtlasPath(string path) {
            if (AtlasPath != null)
                return;
            AtlasPath = path;
        }

        public MTextureOverlay AddOverlay(MTextureOverlay overlay) {
            if (Overlays == null)
                Overlays = new List<MTextureOverlay>();
            Overlays.Add(overlay);
            return overlay;
        }

        public MTextureOverlay AddOverlay(VirtualTexture texture)
            => AddOverlay(texture, DrawOffset, Width, Height);
        public MTextureOverlay AddOverlay(VirtualTexture texture, Vector2 drawOffset, int frameWidth, int frameHeight)
            => AddOverlay(new MTextureOverlay {
                Texture = texture,
                ClipRect = new Rectangle(0, 0, texture.Width, texture.Height),
                ForceClipRect = true,
                DrawOffset = drawOffset,
                Width = frameWidth,
                Height = frameHeight,
            });

        [MonoModReplace]
        public new MTexture GetSubtexture(int x, int y, int width, int height, MTexture applyTo = null) {
            MTexture result;
            if (applyTo == null) {
                result = new MTexture(this, x, y, width, height);
            } else {
                applyTo.AddOverlay(new MTextureParent(this).GetRelativeRect(x, y, width, height));
                applyTo.AddOverlay(new MTextureOverlay {
                    // ClipRect = GetRelativeRect(x, y, width, height),
                    DrawOffset = new Vector2(-Math.Min(x - DrawOffset.X, 0f), -Math.Min(y - DrawOffset.Y, 0f)),
                    Width = width,
                    Height = height,
                });
                result = applyTo;
            }
            return result;
        }

    }
    public static class MTextureExt {

        public static void SetAtlasPath(this MTexture self, string path)
            => ((patch_MTexture) self).SetAtlasPath(path);

        public static MTextureOverlay GetOverlayTexture(this MTexture self)
            => ((patch_MTexture) self).OverlayTexture;

        public static MTextureOverlay GetOverlayMeta(this MTexture self)
            => ((patch_MTexture) self).OverlayMeta;

        public static MTextureOverlay AddOverlay(this MTexture self, MTextureOverlay overlay)
            => ((patch_MTexture) self).AddOverlay(overlay);

        public static MTextureOverlay AddOverlay(this MTexture self, VirtualTexture texture)
            => ((patch_MTexture) self).AddOverlay(texture);

        public static MTextureOverlay AddOverlay(this MTexture self, VirtualTexture texture, Vector2 drawOffset, int frameWidth, int frameHeight)
            => ((patch_MTexture) self).AddOverlay(texture, drawOffset, frameWidth, frameHeight);

    }
}
