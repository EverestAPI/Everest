#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it


using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;
using System;
using System.Xml;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

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

                        Layers.Add(new ImageLayerNoXML(Offset, (patch_Atlas) atlas, layer));
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

        public class patch_ImageLayer : ImageLayer {

            public bool Loop;
            private bool loopDone;
            
            public patch_ImageLayer(Vector2 offset, Atlas atlas, XmlElement xml)
                : base(offset, atlas, xml) {
                //no-op
            }

            public extern void orig_ctor(Vector2 offset, Atlas atlas, XmlElement xml);

            [MonoModConstructor]
            public void ctor(Vector2 offset, Atlas atlas, XmlElement xml) {
                orig_ctor(offset, atlas, xml);

                Loop = xml.AttrBool("loop", true);
            }
            
            public int ImageIndex {
                get {
                    if (Loop) {
                        return (int) (Frame % (float) Images.Count); // as in vanilla
                    } else {
                        if (loopDone) {
                            return Images.Count - 1;
                        } else {
                            int index = (int) (Frame % (float) Images.Count);
                            if (index == Images.Count - 1) {
                                loopDone = true;
                            }
                            return index;
                        }
                    }
                }
            }

            [MonoModIgnore]
            [PatchCompleteRendererImageLayerRender]
            public new extern void Render(Vector2 scroll);
        }

        public class ImageLayerNoXML : patch_ImageLayer {

            public ImageLayerNoXML(Vector2 offset, patch_Atlas atlas, MapMetaCompleteScreenLayer meta)
                : base(offset, atlas, FakeXML) {
                Position = meta.Position + offset;
                ScrollFactor = meta.Scroll;

                Images.Clear();
                foreach (string img in meta.Images) {
                    if (atlas.Has(img)) {
                        Images.Add(atlas[img]);
                    } else {
                        Logger.Warn("Atlas", $"Requested CompleteScreen texture that does not exist: {atlas.DataPath.Substring(17)}/{img}");
                        Images.Add(null);
                    }
                }

                FrameRate = meta.FrameRate;
                Alpha = meta.Alpha;
                Speed = meta.Speed;
                Scale = meta.Scale;
                Loop = meta.Loop;
            }
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to support non-looping complete screen layers.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCompleteRendererImageLayerRender))]
    class PatchCompleteRendererImageLayerRenderAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchCompleteRendererImageLayerRender(ILContext context, CustomAttribute attrib) {
            MethodReference m_ImageLayer_get_ImageIndex = context.Method.DeclaringType.FindProperty("ImageIndex").GetMethod;

            ILCursor cursor = new ILCursor(context);

            // change: MTexture mTexture = Images[(int)(Frame % (float)Images.Count)];
            // to:     MTexture mTexture = Images[ImageIndex];
            // for new property ImageIndex
            cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.CompleteRenderer/ImageLayer", "Images"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_ImageLayer_get_ImageIndex);
            cursor.RemoveRange(8);
        }

    }
}
