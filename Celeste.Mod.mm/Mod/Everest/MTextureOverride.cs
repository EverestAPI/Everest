#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste.Mod {
    public class MTextureOverride {

        public bool IsActiveTexture => !(Texture?.Texture?.IsDisposed ?? true);
        public bool IsActiveMeta => !(Texture?.Texture?.IsDisposed ?? false);

        public VirtualTexture Texture;
        public Rectangle ClipRect;
        public Vector2 DrawOffset;
        public int Width;
        public int Height;

        public bool ForceClipRect = false;

        public virtual void UpdateTexture() {
        }
        public virtual void UpdateMeta() {
        }

    }
}
