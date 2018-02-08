using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Meta {
    public interface IMTextureMeta : IMeta {
        int X { get; set; }
        int Y { get; set; }
        int Width { get; set; }
        int Height { get; set; }
    }
    public class MTextureMeta : IMTextureMeta, ITextureMeta {
        // IMTextureMeta
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        // ITextureMeta
        public bool Premultiplied { get; set; } = false;
    }
}
