#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_CompleteRenderer : CompleteRenderer {

        private MapMetaCompleteScreen meta;

        private static XmlElement _FakeXML;
        public static XmlElement FakeXML {
            get {
                if (FakeXML != null)
                    return FakeXML;
                _FakeXML = new XmlDocument().CreateElement("layer");
                _FakeXML.SetAttribute("img", "");
                return _FakeXML;
            }
        }

        public patch_CompleteRenderer(XmlElement xml, Atlas atlas, float delay, Action onDoneSlide = null)
            : base(xml, atlas, delay, onDoneSlide) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // We're hooking the original constructor, but still need to call it somehow...
        [MonoModLinkTo("Celeste.CompleteRenderer", "System.Void .ctor(System.Xml.XmlElement,Monocle.Atlas,System.Single,System.Action)")]
        public void ctor_CompleteRenderer(XmlElement xml, Atlas atlas, float delay, Action onDoneSlide = null) { }

        // Just adding another true constructor and calling : this(...) would result in a recursive self-invocation.
        [MonoModConstructor]
        public void ctor_CompleteRenderer(XmlElement xml, Atlas atlas, float delay, Action onDoneSlide = null, MapMetaCompleteScreen meta = null) {
            // Translates to : this(xml, atlas, delay, onDoneSlide)
            ctor_CompleteRenderer(xml, atlas, delay, onDoneSlide);

            this.meta = meta;

            if (meta != null) {
                StartScroll = meta.Start;
                CenterScroll = meta.Center;
                Offset = meta.Offset;

                Layers.Clear();
                if (meta.Layers != null && meta.Layers.Length > 0) {
                    foreach (MapMetaCompleteScreenLayer layer in meta.Layers) {
                        if (!string.IsNullOrEmpty(layer.Type) && layer.Type.Equals("ui", StringComparison.CurrentCultureIgnoreCase)) {
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
                this.renderer = renderer;
            }

            public override void Render(Vector2 scroll) {
                renderer?.RenderUI(scroll);
            }
        }

        public class ImageLayerNoXML : ImageLayer {

            public ImageLayerNoXML(Vector2 offset, Atlas atlas, MapMetaCompleteScreenLayer meta)
                : base(offset, atlas, FakeXML) {
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
            }

            public override void Update(Scene scene) {
                Frame += Engine.DeltaTime * FrameRate;
                Offset += Speed * Engine.DeltaTime;
            }

            public override void Render(Vector2 scroll) {
                Vector2 position = GetScrollPosition(scroll).Floor();
                MTexture texture = Images[(int) (Frame % Images.Count)];
                if (texture == null)
                    return;
                Draw.SpriteBatch.Draw(
                    texture.Texture.Texture,
                    position + texture.DrawOffset,
                    new Rectangle(
                        -((int) Offset.X) + 1,
                        -((int) Offset.Y) + 1,
                        texture.ClipRect.Width - 2,
                        texture.ClipRect.Height - 2
                    ),
                    Color.White * Alpha
                );
            }

        }

    }
}
