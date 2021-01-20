namespace Celeste.Mod.Meta {
    public interface ITextureMeta : IMeta {
        bool Premultiplied { get; set; }
    }
    public class TextureMeta {
        // ITextureMeta
        public bool Premultiplied { get; set; } = false;
    }
}
