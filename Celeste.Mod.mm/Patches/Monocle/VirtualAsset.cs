#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
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
    // This is only required as VirtualAsset's members are internal or even private, not protected.
    // Noel or Matt, if you see this, please change the visibility to protected. Thanks!
    [MonoModIgnore]
    abstract class patch_VirtualAsset {

        public string Name { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }

        internal virtual void Unload() {
        }

        internal virtual void Reload() {
        }

    }
}
