using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;

namespace Celeste
{
    // : Solid because base.Awake
    class patch_StarJumpBlock : Solid
    {
        private Level level;

        public patch_StarJumpBlock(Vector2 position, float width, float height, bool sinks) : base(position, width, height, sinks)
        {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        public override void Awake(Scene scene)
        {
            base.Awake(scene);
            this.level = this.SceneAs<Level>();
            List<MTexture> atlasSubtextures1 = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/leftrailing");
            List<MTexture> atlasSubtextures2 = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/railing");
            List<MTexture> atlasSubtextures3 = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/rightrailing");
            List<MTexture> atlasSubtextures4 = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/edgeH");
            for (int index = 8; (double)index < (double)this.Width - 8.0; index += 8)
            {
                if (this.Open((float)index, -8f))
                {
                    Monocle.Image image1 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures4));
                    image1.CenterOrigin();
                    image1.Position = new Vector2((float)(index + 4), 4f);
                    this.Add((Component)image1);
                    Monocle.Image image2 = new Monocle.Image(atlasSubtextures2[this.mod((int)((double)this.X + (double)index) / 8, atlasSubtextures2.Count)]);
                    image2.Position = new Vector2((float)index, -8f);
                    this.Add((Component)image2);
                }
                if (this.Open((float)index, this.Height))
                {
                    Monocle.Image image = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures4));
                    image.CenterOrigin();
                    image.Scale.Y = -1f;
                    image.Position = new Vector2((float)(index + 4), this.Height - 4f);
                    this.Add((Component)image);
                }
            }
            List<MTexture> atlasSubtextures5 = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/edgeV");
            for (int index = 8; (double)index < (double)this.Height - 8.0; index += 8)
            {
                if (this.Open(-8f, (float)index))
                {
                    Monocle.Image image = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures5));
                    image.CenterOrigin();
                    image.Scale.X = -1f;
                    image.Position = new Vector2(4f, (float)(index + 4));
                    this.Add((Component)image);
                }
                if (this.Open(this.Width, (float)index))
                {
                    Monocle.Image image = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures5));
                    image.CenterOrigin();
                    image.Position = new Vector2(this.Width - 4f, (float)(index + 4));
                    this.Add((Component)image);
                }
            }
            List<MTexture> atlasSubtextures6 = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/corner");
            Monocle.Image image3 = (Monocle.Image)null;
            if (this.Open(-8f, 0.0f) && this.Open(0.0f, -8f))
            {
                image3 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures6));
                image3.Scale.X = -1f;
                Monocle.Image image1 = new Monocle.Image(atlasSubtextures1[this.mod((int)this.X / 8, atlasSubtextures1.Count)]);
                image1.Position = new Vector2(0.0f, -8f);
                this.Add((Component)image1);
            }
            else if (this.Open(-8f, 0.0f))
            {
                image3 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures5));
                image3.Scale.X = -1f;
            }
            else if (this.Open(0.0f, -8f))
            {
                image3 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures4));
                Monocle.Image image1 = new Monocle.Image(atlasSubtextures2[this.mod((int)this.X / 8, atlasSubtextures2.Count)]);
                image1.Position = new Vector2(0.0f, -8f);
                this.Add((Component)image1);
            }
            if (image3 != null)
            {
                image3.CenterOrigin();
                image3.Position = new Vector2(4f, 4f);
                this.Add((Component)image3);
            }
            Monocle.Image image4 = (Monocle.Image)null;
            if (this.Open(this.Width, 0.0f) && this.Open(this.Width - 8f, -8f))
            {
                image4 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures6));
                Monocle.Image image1 = new Monocle.Image(atlasSubtextures3[this.mod((int)((double)this.X + (double)this.Width) / 8 - 1, atlasSubtextures3.Count)]);
                image1.Position = new Vector2(this.Width - 8f, -8f);
                this.Add((Component)image1);
            }
            else if (this.Open(this.Width, 0.0f))
                image4 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures5));
            else if (this.Open(this.Width - 8f, -8f))
            {
                image4 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures4));
                Monocle.Image image1 = new Monocle.Image(atlasSubtextures2[this.mod((int)((double)this.X + (double)this.Width) / 8 - 1, atlasSubtextures2.Count)]);
                image1.Position = new Vector2(this.Width - 8f, -8f);
                this.Add((Component)image1);
            }
            if (image4 != null)
            {
                image4.CenterOrigin();
                image4.Position = new Vector2(this.Width - 4f, 4f);
                this.Add((Component)image4);
            }
            Monocle.Image image5 = (Monocle.Image)null;
            if (this.Open(-8f, this.Height - 8f) && this.Open(0.0f, this.Height))
            {
                image5 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures6));
                image5.Scale.X = -1f;
            }
            else if (this.Open(-8f, this.Height - 8f))
            {
                image5 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures5));
                image5.Scale.X = -1f;
            }
            else if (this.Open(0.0f, this.Height))
                image5 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures4));
            if (image5 != null)
            {
                image5.Scale.Y = -1f;
                image5.CenterOrigin();
                image5.Position = new Vector2(4f, this.Height - 4f);
                this.Add((Component)image5);
            }
            Monocle.Image image6 = (Monocle.Image)null;
            if (this.Open(this.Width, this.Height - 8f) && this.Open(this.Width - 8f, this.Height))
                image6 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures6));
            else if (this.Open(this.Width, this.Height - 8f))
                image6 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures5));
            else if (this.Open(this.Width - 8f, this.Height))
                image6 = new Monocle.Image(Calc.Random.Choose<MTexture>(atlasSubtextures4));
            if (image6 != null)
            {
                image6.Scale.Y = -1f;
                image6.CenterOrigin();
                image6.Position = new Vector2(this.Width - 4f, this.Height - 4f);
                this.Add((Component)image6);
            }
        }

        [MonoModIgnore]
        private extern int mod(int v, int count);

        [MonoModIgnore]
        private extern bool Open(float index, float v);
    }
}
