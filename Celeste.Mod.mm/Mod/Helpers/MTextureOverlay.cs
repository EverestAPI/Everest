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
    public class MTextureOverlay {

        public virtual bool IsActiveTexture => Texture != null && Texture.Texture != null && !Texture.Texture.IsDisposed;
        public virtual bool IsActiveMeta => Texture == null || (Texture.Texture != null && !Texture.Texture.IsDisposed);

        public virtual VirtualTexture Texture { get; set; }
        public virtual Rectangle ClipRect { get; set; }
        public virtual string AtlasPath { get; set; }
        public virtual Vector2 DrawOffset { get; set; }
        public virtual int Width { get; set; }
        public virtual int Height { get; set; }

        public virtual bool ForceClipRect { get; set; } = false;


    }
    public class MTextureParent : MTextureOverlay {

        public MTexture Parent;

        public override bool IsActiveTexture => Parent != null && base.IsActiveTexture;
        public override bool IsActiveMeta => Parent != null && base.IsActiveMeta;

        public override VirtualTexture Texture => Parent.Texture;
        public override Rectangle ClipRect {
            get {
                Rectangle parentRect = Parent.ClipRect;
                if (!HasRelativeRect)
                    return parentRect;

                if (Parent.GetOverlayTexture()?.ForceClipRect ?? false) {
                    // TODO: Uh... UV-based clip rect calculation?
                }

                Vector2 parentOffset = Parent.DrawOffset;

                int a = (int) (parentRect.X - parentOffset.X + RelativeRectX);
                int b = (int) (parentRect.Y - parentOffset.Y + RelativeRectY);
                int x = (int) MathHelper.Clamp(a, parentRect.Left, parentRect.Right);
                int y = (int) MathHelper.Clamp(b, parentRect.Top, parentRect.Bottom);
                int width = Math.Max(0, Math.Min(a + RelativeRectWidth, parentRect.Right) - x);
                int height = Math.Max(0, Math.Min(b + RelativeRectHeight, parentRect.Bottom) - y);
                return new Rectangle(x, y, width, height);
            }
        }
        public override string AtlasPath => Parent.AtlasPath;
        public override Vector2 DrawOffset => Parent.DrawOffset;
        public override int Width => Parent.Width;
        public override int Height => Parent.Height;

        protected bool HasRelativeRect;
        protected int RelativeRectX;
        protected int RelativeRectY;
        protected int RelativeRectWidth;
        protected int RelativeRectHeight;

        public MTextureParent()
            : this(null) {
        }
        public MTextureParent(MTexture inner) {
            Parent = inner;
        }

        public MTextureParent GetRelativeRect(Rectangle rect)
            => GetRelativeRect(rect.X, rect.Y, rect.Width, rect.Height);
        public MTextureParent GetRelativeRect(int x, int y, int width, int height) {
            HasRelativeRect = true;
            RelativeRectX = x;
            RelativeRectY = y;
            RelativeRectWidth = width;
            RelativeRectHeight = height;
            return this;
        }

    }
}
