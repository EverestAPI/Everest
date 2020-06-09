using Celeste.Mod.Meta;
using Monocle;
using System.IO;
using System.Xml;

namespace Celeste.Mod.Entities {
    public class CustomScreenVignette : Scene {
        private Session session;
        private CompleteRenderer renderer;
        private bool addedRenderer;

        private MapMetaCompleteScreen meta;
        private XmlElement xml;

        private bool ready;
        private bool slideFinished;
        private bool ending;

        public CustomScreenVignette(Session session, XmlElement xml = null, MapMetaCompleteScreen meta = null) {
            this.session = session;
            session.Audio.Apply();
            this.meta = meta;
            this.xml = xml;

            RunThread.Start(LoadCompleteThread, "SUMMIT_VIGNETTE");
        }

        private void LoadCompleteThread() {
            Atlas atlas = null;
            if (meta != null)
                atlas = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", meta.Atlas), Atlas.AtlasDataFormat.PackerNoAtlas);
            else
                atlas = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", xml.Attr("atlas")), Atlas.AtlasDataFormat.PackerNoAtlas);

            renderer = new patch_CompleteRenderer(xml, atlas, 0f, () => slideFinished = true, meta);
            renderer.SlideDuration = 7.5f;

            ready = true;
        }

        public override void Update() {
            if (ready && !addedRenderer) {
                Add(renderer);
                addedRenderer = true;
            }
            base.Update();
            if ((Input.MenuConfirm.Pressed || slideFinished) && !ending && ready) {
                ending = true;
                AreaData.Get(session).DoScreenWipe(this, false, () => Engine.Scene = new LevelLoader(session));
            }
        }

        public override void End() {
            base.End();
            renderer?.Dispose();
        }

    }
}
