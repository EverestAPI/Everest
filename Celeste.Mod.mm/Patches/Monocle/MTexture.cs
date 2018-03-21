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

        public extern VirtualTexture orig_get_Texture();
        public extern void orig_set_Texture(VirtualTexture value);
        protected VirtualTexture _Texture {
            get {
                return OverrideTexture?.Texture ?? Parent?.Texture ?? orig_get_Texture();
            }
        }
        public new VirtualTexture Texture {
            get {
                VirtualTexture texture = _Texture;
                // Reset caches whenever the texture is used by the game, f.e. on render.
                _CachedOverrideTexture = null;
                _CachedOverrideMeta = null;
                return texture;
            }
            set {
                if (OverrideTexture != null)
                    OverrideTexture.Texture = value;
                else
                    orig_set_Texture(value);
            }
        }
        public extern Rectangle orig_get_ClipRect();
        public extern void orig_set_ClipRect(Rectangle value);
        public new Rectangle ClipRect {
            get {
                MTextureOverride layer = OverrideTexture;
                if (layer != null && layer.ForceClipRect)
                    return layer.ClipRect;

                if (Parent != null) {
                    Rectangle parentRect = Parent.ClipRect;
                    if (!HasRelativeRect)
                        return parentRect;

                    Vector2 parentOffset = Parent.DrawOffset;

                    // TODO: Possible perf bottleneck?
                    int a = (int) (parentRect.X - parentOffset.X + RelativeRectX);
                    int b = (int) (parentRect.Y - parentOffset.Y + RelativeRectY);
                    int x = (int) MathHelper.Clamp(a, parentRect.Left, parentRect.Right);
                    int y = (int) MathHelper.Clamp(b, parentRect.Top, parentRect.Bottom);
                    int width = Math.Max(0, Math.Min(a + RelativeRectWidth, parentRect.Right) - x);
                    int height = Math.Max(0, Math.Min(b + RelativeRectHeight, parentRect.Bottom) - y);
                    return new Rectangle(x, y, width, height);
                }

                return layer?.ClipRect ?? orig_get_ClipRect();
            }
            set {
                if (OverrideTexture != null)
                    OverrideTexture.ClipRect = value;
                else
                    orig_set_ClipRect(value);
            }
        }
        public extern Vector2 orig_get_DrawOffset();
        public extern void orig_set_DrawOffset(Vector2 value);
        public new Vector2 DrawOffset {
            get {
                return OverrideMeta?.DrawOffset ?? orig_get_DrawOffset();
            }
            set {
                if (OverrideMeta != null)
                    OverrideMeta.DrawOffset = value;
                else
                    orig_set_DrawOffset(value);
            }
        }
        public extern int orig_get_Width();
        public extern void orig_set_Width(int value);
        public new int Width {
            get {
                return OverrideMeta?.Width ?? orig_get_Width();
            }
            set {
                if (OverrideMeta != null)
                    OverrideMeta.Width = value;
                else
                    orig_set_Width(value);
            }
        }
        public extern int orig_get_Height();
        public extern void orig_set_Height(int value);
        public new int Height {
            get {
                return OverrideMeta?.Height ?? orig_get_Height();
            }
            set {
                if (OverrideMeta != null)
                    OverrideMeta.Height = value;
                else
                    orig_set_Height(value);
            }
        }

        public new Vector2 Center => new Vector2(Width * 0.5f, Height * 0.5f);
        public new float LeftUV => ClipRect.Left / (float) _Texture.Width;
        public new float RightUV => ClipRect.Right / (float) _Texture.Width;
        public new float TopUV => ClipRect.Top / (float) _Texture.Height;
        public new float BottomUV => ClipRect.Bottom / (float) _Texture.Height;

        protected int _OverrideCount = 0;
        protected MTextureOverride[] _Overrides;

        protected MTextureOverride _CachedOverrideTexture;
        public MTextureOverride OverrideTexture {
            get {
                if (!(_CachedOverrideTexture?.Texture?.IsDisposed ?? true))
                    return _CachedOverrideTexture;
                if (_OverrideCount == 0)
                    return null;
                for (int i = _OverrideCount - 1; i > -1; --i) {
                    MTextureOverride layer = _Overrides[i];
                    layer.UpdateTexture();
                    if (layer.IsActiveTexture)
                        return _CachedOverrideTexture = layer;
                }
                return null;
            }
        }
        protected MTextureOverride _CachedOverrideMeta;
        public MTextureOverride OverrideMeta {
            get {
                if (_CachedOverrideMeta != null)
                    return _CachedOverrideMeta;
                if (_OverrideCount == 0)
                    return null;
                for (int i = _OverrideCount - 1; i > -1; --i) {
                    MTextureOverride layer = _Overrides[i];
                    layer.UpdateMeta();
                    if (layer.IsActiveMeta)
                        return _CachedOverrideMeta = layer;
                }
                return null;
            }
        }
        public MTexture Parent;

        protected bool HasRelativeRect;
        protected int RelativeRectX;
        protected int RelativeRectY;
        protected int RelativeRectWidth;
        protected int RelativeRectHeight;

        public extern string orig_get_AtlasPath();
        public extern void orig_set_AtlasPath(string value);
        public new string AtlasPath {
            get {
                return OverrideMeta?.AtlasPath ?? Parent?.AtlasPath ?? orig_get_AtlasPath();
            }
            set {
                if (OverrideMeta != null)
                    OverrideMeta.AtlasPath = value;
                else
                    orig_set_AtlasPath(value);
            }
        }

        // Patching constructors is ugly.
        public extern void orig_ctor_MTexture(MTexture parent, int x, int y, int width, int height);
        [MonoModConstructor]
        public void ctor_MTexture(MTexture parent, int x, int y, int width, int height) {
            orig_ctor_MTexture(parent, x, y, width, height);
            Parent = parent;
            HasRelativeRect = true;
            RelativeRectX = x;
            RelativeRectY = y;
            RelativeRectWidth = width;
            RelativeRectHeight = height;
        }

        public extern void orig_ctor_MTexture(MTexture parent, string atlasPath, Rectangle clipRect, Vector2 drawOffset, int width, int height);
        [MonoModConstructor]
        public void ctor_MTexture(MTexture parent, string atlasPath, Rectangle clipRect, Vector2 drawOffset, int width, int height) {
            orig_ctor_MTexture(parent, atlasPath, clipRect, drawOffset, width, height);
            Parent = parent;
            HasRelativeRect = true;
            RelativeRectX = clipRect.X;
            RelativeRectY = clipRect.Y;
            RelativeRectWidth = clipRect.Width;
            RelativeRectHeight = clipRect.Height;
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

        public MTextureOverride AddOverride(MTextureOverride layer) {
            _CachedOverrideMeta = null;
            _CachedOverrideTexture = null;

            if (layer == null)
                return null;

            if (_Overrides == null)
                _Overrides = new MTextureOverride[16];

            if (_OverrideCount + 1 >= _Overrides.Length) {
                MTextureOverride[] grown = new MTextureOverride[_Overrides.Length * 2];
                Array.Copy(_Overrides, grown, _Overrides.Length);
                _Overrides = grown;
            }
            _Overrides[_OverrideCount] = layer;
            ++_OverrideCount;

            return layer;
        }

        public MTextureOverride AddOverride(VirtualTexture texture)
            => AddOverride(texture, DrawOffset, Width, Height);
        public MTextureOverride AddOverride(VirtualTexture texture, Vector2 drawOffset, int frameWidth, int frameHeight)
            => AddOverride(new MTextureOverride {
                Texture = texture,
                ClipRect = new Rectangle(0, 0, texture.Width, texture.Height),
                ForceClipRect = true,
                DrawOffset = drawOffset,
                Width = frameWidth,
                Height = frameHeight,
            });

        [MonoModReplace]
        public new MTexture GetSubtexture(int x, int y, int width, int height, MTexture applyTo = null) {
            if (applyTo == null) {
                return new MTexture(this, x, y, width, height);
            }

            patch_MTexture sub = (patch_MTexture) applyTo;
            sub.Parent = this;
            sub.HasRelativeRect = true;
            sub.RelativeRectX = x;
            sub.RelativeRectY = y;
            sub.RelativeRectWidth = width;
            sub.RelativeRectHeight = height;
            sub.DrawOffset = new Vector2(-Math.Min(x - DrawOffset.X, 0f), -Math.Min(y - DrawOffset.Y, 0f));
            sub.Width = width;
            sub.Height = height;
            return sub;
        }

    }
    public static class MTextureExt {

        /// <summary>
        /// Set the AtlasPath property for this MTexture. Can only be set if it is null.
        /// </summary>
        public static void SetAtlasPath(this MTexture self, string path)
            => ((patch_MTexture) self).SetAtlasPath(path);

        /// <summary>
        /// Get the currently active texture override.
        /// </summary>
        public static MTextureOverride GetOverrideTexture(this MTexture self)
            => ((patch_MTexture) self).OverrideTexture;

        /// <summary>
        /// Get the currently active meta override.
        /// </summary>
        public static MTextureOverride GetOverrideMeta(this MTexture self)
            => ((patch_MTexture) self).OverrideMeta;

        /// <summary>
        /// Add a new override layer.
        /// </summary>
        public static MTextureOverride AddOverride(this MTexture self, MTextureOverride layer)
            => ((patch_MTexture) self).AddOverride(layer);

        /// <summary>
        /// Add a new override layer, based on the given VirtualTexture.
        /// </summary>
        /// <returns>The resulting MTextureOverride.</returns>
        public static MTextureOverride AddOverride(this MTexture self, VirtualTexture texture)
            => ((patch_MTexture) self).AddOverride(texture);

        /// <summary>
        /// Add a new override layer, based on the given VirtualTexture and parameters.
        /// </summary>
        /// <returns>The resulting MTextureOverride.</returns>
        public static MTextureOverride AddOverride(this MTexture self, VirtualTexture texture, Vector2 drawOffset, int frameWidth, int frameHeight)
            => ((patch_MTexture) self).AddOverride(texture, drawOffset, frameWidth, frameHeight);

        /// <summary>
        /// Gets the parent texture of the given MTexture.
        /// </summary>
        public static MTexture GetParent(this MTexture self)
            => ((patch_MTexture) self).Parent;

        /// <summary>
        /// Sets the parent texture of the given MTexture.
        /// </summary>
        public static void SetParent(this MTexture self, MTexture parent)
            => ((patch_MTexture) self).Parent = parent;

    }
}
