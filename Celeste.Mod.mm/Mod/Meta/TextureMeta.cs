using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Meta {
    public interface ITextureMeta : IMeta {
        bool Premultiplied { get; set; }
    }
    public class TextureMeta {
        // ITextureMeta
        public bool Premultiplied { get; set; } = false;
    }
}
