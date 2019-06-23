#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value 0

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;
using System.IO;
using FMOD.Studio;
using Monocle;
using Celeste.Mod.Meta;

namespace Celeste {
    class patch_CrystalStaticSpinner : CrystalStaticSpinner {
        
        // We're effectively in CrystalStaticSpinner, but still need to "expose" private fields to our mod.
        private CrystalColor color;
        private bool expanded;
        private int randomSeed;
        private Entity filler;
        private patch_CrystalStaticSpinner.Border border;
        private static Dictionary<CrystalColor, string> fgTextureLookup;
        private static Dictionary<CrystalColor, string> bgTextureLookup;
        private bool SolidCheck(Vector2 position)
        {
            if (this.AttachToSolid)
            {
                return false;
            }
            using (List<Solid>.Enumerator enumerator = base.Scene.CollideAll<Solid>(position).GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is SolidTiles)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public string directory;
        public string tint;

        public patch_CrystalStaticSpinner(Vector2 position, bool attachToSolid, CrystalColor color)
            : base(position, attachToSolid, color) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset, CrystalColor color);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset, CrystalColor color)
        {
            orig_ctor(data, offset, color);

            this.directory = data.Attr("directory", "");
            this.tint = data.Attr("tint", "ffffff");
        }

        public extern void orig_Awake(Scene scene);
        public override void Awake(Scene scene) {
            if ((int) color == -1) {
                Add(new CoreModeListener(this));
                if ((scene as Level).CoreMode == Session.CoreModes.Cold) {
                    color = CrystalColor.Blue;
                } else {
                    color = CrystalColor.Red;
                }
            }

            orig_Awake(scene);
        }

        public extern void orig_CreateSprites();
        public void CreateSprites()
        {              
            if (!this.expanded)
            {
                Calc.PushRandom(this.randomSeed);
                List<MTexture> atlasSubtextures;
                if (this.directory == "")
                {
                    atlasSubtextures = GFX.Game.GetAtlasSubtextures(patch_CrystalStaticSpinner.fgTextureLookup[this.color]);
                } else
                {
                    atlasSubtextures = GFX.Game.GetAtlasSubtextures(this.directory + "/fg");
                }    
                MTexture mtexture = Calc.Random.Choose(atlasSubtextures);
                Image image;
                if (!this.SolidCheck(new Vector2(base.X - 4f, base.Y - 4f)))
                {
                    image = new Image(mtexture.GetSubtexture(0, 0, 14, 14, null)).SetOrigin(12f, 12f);
                    image.Color = Calc.HexToColor(this.tint);
                    base.Add(image);
                }
                if (!this.SolidCheck(new Vector2(base.X + 4f, base.Y - 4f)))
                {
                    image = new Image(mtexture.GetSubtexture(10, 0, 14, 14, null)).SetOrigin(2f, 12f);
                    image.Color = Calc.HexToColor(this.tint);
                    base.Add(image);
                }
                if (!this.SolidCheck(new Vector2(base.X + 4f, base.Y + 4f)))
                {
                    image = new Image(mtexture.GetSubtexture(10, 10, 14, 14, null)).SetOrigin(2f, 2f);
                    image.Color = Calc.HexToColor(this.tint);
                    base.Add(image);
                }
                if (!this.SolidCheck(new Vector2(base.X - 4f, base.Y + 4f)))
                {
                    image = new Image(mtexture.GetSubtexture(0, 10, 14, 14, null)).SetOrigin(12f, 2f);
                    image.Color = Calc.HexToColor(this.tint);
                    base.Add(image);
                }
                foreach (Entity entity in base.Scene.Tracker.GetEntities<CrystalStaticSpinner>())
                {
                    CrystalStaticSpinner crystalStaticSpinner = (CrystalStaticSpinner)entity;
                    if (crystalStaticSpinner != this && crystalStaticSpinner.AttachToSolid == this.AttachToSolid && crystalStaticSpinner.X >= base.X && (crystalStaticSpinner.Position - this.Position).Length() < 24f)
                    {
                        this.AddSprite((this.Position + crystalStaticSpinner.Position) / 2f - this.Position);
                    }
                }
                base.Scene.Add(this.border = new patch_CrystalStaticSpinner.Border(this, this.filler));
                this.expanded = true;
                Calc.PopRandom();
            }
        }

        public extern void orig_AddSprite(Vector2 offset);
        private void AddSprite(Vector2 offset)
        {
            if (this.filler == null)
            {
                base.Scene.Add(this.filler = new Entity(this.Position));
                this.filler.Depth = base.Depth + 1;
            }
            
            List<MTexture> atlasSubtextures;
            if (this.directory == "")
            {
                atlasSubtextures = GFX.Game.GetAtlasSubtextures(patch_CrystalStaticSpinner.bgTextureLookup[this.color]);
            } else
            {
                atlasSubtextures = GFX.Game.GetAtlasSubtextures(this.directory + "/bg");
            }
            Image image = new Image(Calc.Random.Choose(atlasSubtextures));
            image.Position = offset;
            image.Rotation = (float)Calc.Random.Choose(0, 1, 2, 3) * 1.57079637f;
            image.CenterOrigin();
            image.Color = Calc.HexToColor(this.tint);
            this.filler.Add(image);
        }

        [MonoModIgnore]
        private class CoreModeListener : Component {
            public CoreModeListener(CrystalStaticSpinner parent)
                : base(true, false) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }
        }

        // idk how to fix "CrystalStaticSpinner.Border is innaccesible due to it's protection level", so I'll just do this.
        private class Border : Entity
        {
            public Border(Entity parent, Entity filler)
            {
                this.drawing = new Entity[2];
                this.drawing[0] = parent;
                this.drawing[1] = filler;
                base.Depth = parent.Depth + 2;
            }

            public override void Render()
            {
                if (!this.drawing[0].Visible)
                {
                    return;
                }
                this.DrawBorder(this.drawing[0]);
                this.DrawBorder(this.drawing[1]);
            }

            private void DrawBorder(Entity entity)
            {
                if (entity == null)
                {
                    return;
                }
                foreach (Component component in entity.Components)
                {
                    Image image = component as Image;
                    if (image != null)
                    {
                        Color color = image.Color;
                        Vector2 position = image.Position;
                        image.Color = Color.Black;
                        image.Position = position + new Vector2(0f, -1f);
                        image.Render();
                        image.Position = position + new Vector2(0f, 1f);
                        image.Render();
                        image.Position = position + new Vector2(-1f, 0f);
                        image.Render();
                        image.Position = position + new Vector2(1f, 0f);
                        image.Render();
                        image.Color = color;
                        image.Position = position;
                    }
                }
            }
            private Entity[] drawing;
        }
    }
}
