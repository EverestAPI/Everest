#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value


using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;
using System;
using System.Xml;

namespace Celeste {
    class patch_CompleteRenderer : CompleteRenderer {

        [MonoModIgnore]
        public new bool HasUI { get; private set; }

        private MapMetaCompleteScreen meta;

        private static XmlElement _FakeXML;
        public static XmlElement FakeXML {
            get {
                if (_FakeXML != null)
                    return _FakeXML;
                _FakeXML = new XmlDocument().CreateElement("layer");
                _FakeXML.SetAttribute("img", "");
                return _FakeXML;
            }
        }

        public patch_CompleteRenderer(XmlElement xml, Atlas atlas, float delay, Action onDoneSlide = null, MapMetaCompleteScreen meta = null)
            : base(xml, atlas, delay, onDoneSlide) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            // meta parameter is included to allow instantiating CompleteRenderer with it
        }

        // We're hooking the original constructor, but still need to call it somehow...
        [MonoModLinkTo("Celeste.CompleteRenderer", "System.Void .ctor(System.Xml.XmlElement,Monocle.Atlas,System.Single,System.Action)")]
        [MonoModRemove]
        public extern void ctor(XmlElement xml, Atlas atlas, float delay, Action onDoneSlide = null);

        // Just adding another true constructor and calling : this(...) would result in a recursive self-invocation.
        [MonoModConstructor]
        public void ctor(XmlElement xml, Atlas atlas, float delay, Action onDoneSlide = null, MapMetaCompleteScreen meta = null) {
            // Translates to : this(xml, atlas, delay, onDoneSlide)
            ctor(xml, atlas, delay, onDoneSlide);

            this.meta = meta;

            if (meta != null) {
                StartScroll = meta.Start;
                CenterScroll = meta.Center;
                Offset = meta.Offset;

                Layers.Clear();
                if (meta.Layers != null && meta.Layers.Length > 0) {
                    foreach (MapMetaCompleteScreenLayer layer in meta.Layers) {
                        if (!string.IsNullOrEmpty(layer.Type) && layer.Type.Equals("ui", StringComparison.CurrentCultureIgnoreCase)) {
                            HasUI = true;
                            Layers.Add(new UILayerNoXML(this, layer));
                            continue;
                        }

                        Layers.Add(new ImageLayerNoXML(Offset, atlas, layer));
                    }
                }
            }

            // Let's just hope that the running SlideRoutine takes the changes into account.
        }

        public abstract class LayerNoXML : Layer {

            public LayerNoXML(MapMetaCompleteScreenLayer meta)
                : base(FakeXML) {
                Position = meta.Position;
                ScrollFactor = meta.Scroll;
            }

        }

        public class UILayerNoXML : UILayer {

            private CompleteRenderer renderer;

            public UILayerNoXML(CompleteRenderer renderer, MapMetaCompleteScreenLayer meta)
                : base(renderer, FakeXML) {
                Position = meta.Position;
                ScrollFactor = meta.Scroll;

                this.renderer = renderer;
            }

            public override void Render(Vector2 scroll) {
                renderer?.RenderUI(scroll);
            }

        }

        public class ImageLayerNoXML : ImageLayer {

            public ImageLayerNoXML(Vector2 offset, Atlas atlas, MapMetaCompleteScreenLayer meta)
                : base(offset, atlas, FakeXML) {
                Position = meta.Position;
                ScrollFactor = meta.Scroll;

                Images.Clear();
                foreach (string img in meta.Images) {
                    if (atlas.Has(img)) {
                        Images.Add(atlas[img]);
                    } else {
                        Images.Add(null);
                    }
                }

                FrameRate = meta.FrameRate;
                Alpha = meta.Alpha;
                Speed = meta.Speed;
                Scale = meta.Scale;
            }
        }

    }
}
