#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    class patch_StarJumpBlock : StarJumpBlock {
        private Level level;

        public patch_StarJumpBlock(Vector2 position, float width, float height, bool sinks) : base(position, width, height, sinks) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModLinkTo("Celeste.Solid", "Awake")]
        [MonoModIgnore]
        public extern void base_Awake(Scene scene);
        public extern void orig_Awake(Scene scene);
        public override void Awake(Scene scene) {
            if ((scene as Level).Session.Area.GetLevelSet() == "Celeste") {
                orig_Awake(scene);
                return;
            }

            // This method could've been patched via an IL hook, but eh.
            // The vanilla method is missing support for inner corners.

            // TODO: Inner corner textures? Or keep them empty as-is?

            base_Awake(scene);

            level = SceneAs<Level>();

            List<MTexture> railsL = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/leftrailing");
            List<MTexture> rails = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/railing");
            List<MTexture> railsR = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/rightrailing");
            List<MTexture> edgesH = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/edgeH");
            List<MTexture> edgesV = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/edgeV");
            List<MTexture> corners = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/corner");

            // Top and bottom edges.
            for (int i = 8; i < Width - 8f; i += 8) {
                if (Open(i, -8f)) {
                    Image edge = new Image(Calc.Random.Choose(edgesH));
                    edge.CenterOrigin();
                    edge.Position = new Vector2(i + 4, 4f);
                    Add(edge);

                    Image rail = new Image(rails[mod((int) (X + i) / 8, rails.Count)]);
                    rail.Position = new Vector2(i, -8f);
                    Add(rail);
                }

                if (Open(i, Height)) {
                    Image edge = new Image(Calc.Random.Choose(edgesH));
                    edge.CenterOrigin();
                    edge.Scale.Y = -1f;
                    edge.Position = new Vector2(i + 4, Height - 4f);
                    Add(edge);
                }
            }

            // Left and right edges.
            for (int i = 8; i < Height - 8f; i += 8) {
                if (Open(-8f, i)) {
                    Image edge = new Image(Calc.Random.Choose(edgesV));
                    edge.CenterOrigin();
                    edge.Scale.X = -1f;
                    edge.Position = new Vector2(4f, i + 4);
                    Add(edge);
                }

                if (Open(Width, i)) {
                    Image edge = new Image(Calc.Random.Choose(edgesV));
                    edge.CenterOrigin();
                    edge.Position = new Vector2(Width - 4f, i + 4);
                    Add(edge);
                }
            }


            // Corners / additional edges.

            Image img;

            img = null;
            if (Open(-8f, 0f) && Open(0f, -8f)) {
                img = new Image(Calc.Random.Choose(corners));
                img.Scale.X = -1f;

                Image rail = new Image(railsL[mod((int) X / 8, railsL.Count)]);
                rail.Position = new Vector2(0f, -8f);
                Add(rail);

            } else if (Open(-8f, 0f)) {
                img = new Image(Calc.Random.Choose(edgesV));
                img.Scale.X = -1f;

            } else if (Open(0f, -8f)) {
                img = new Image(Calc.Random.Choose(edgesH));

                Image rail = new Image(rails[mod((int) X / 8, rails.Count)]);
                rail.Position = new Vector2(0f, -8f);
                Add(rail);
            }

            if (img != null) {
                img.CenterOrigin();
                img.Position = new Vector2(4f, 4f);
                Add(img);
            }


            img = null;
            if (Open(Width, 0f) && Open(Width - 8f, -8f)) {
                img = new Image(Calc.Random.Choose(corners));

                Image rail = new Image(railsR[mod((int) (X + Width) / 8 - 1, railsR.Count)]);
                rail.Position = new Vector2(Width - 8f, -8f);
                Add(rail);

            } else if (Open(Width, 0f)) {
                img = new Image(Calc.Random.Choose(edgesV));

            } else if (Open(Width - 8f, -8f)) {
                img = new Image(Calc.Random.Choose(edgesH));

                Image rail = new Image(rails[mod((int) (X + Width) / 8 - 1, rails.Count)]);
                rail.Position = new Vector2(Width - 8f, -8f);
                Add(rail);
            }

            if (img != null) {
                img.CenterOrigin();
                img.Position = new Vector2(Width - 4f, 4f);
                Add(img);
            }


            img = null;
            if (Open(-8f, Height - 8f) && Open(0f, Height)) {
                img = new Image(Calc.Random.Choose(corners));
                img.Scale.X = -1f;

            } else if (Open(-8f, Height - 8f)) {
                img = new Image(Calc.Random.Choose(edgesV));
                img.Scale.X = -1f;

            } else if (Open(0f, Height)) {
                img = new Image(Calc.Random.Choose(edgesH));
            }

            if (img != null) {
                img.Scale.Y = -1f;
                img.CenterOrigin();
                img.Position = new Vector2(4f, Height - 4f);
                Add(img);
            }


            img = null;
            if (Open(Width, Height - 8f) && Open(Width - 8f, Height)) {
                img = new Image(Calc.Random.Choose(corners));

            } else if (Open(Width, Height - 8f)) {
                img = new Image(Calc.Random.Choose(edgesV));

            } else if (Open(Width - 8f, Height)) {
                img = new Image(Calc.Random.Choose(edgesH));
            }

            if (img != null) {
                img.Scale.Y = -1f;
                img.CenterOrigin();
                img.Position = new Vector2(Width - 4f, Height - 4f);
                Add(img);
            }
        }

        [MonoModLinkTo("Monocle.Entity", "Render")]
        [MonoModIgnore]
        public extern void base_Render();
        public extern void orig_Render();
        public override void Render() {
            if (SceneAs<Level>().Session.Area.GetLevelSet() == "Celeste") {
                orig_Render();
                return;
            }

            // Allow the StarJumpBlock to use an Everest StarJumpGraphicsController instead,
            // which is color-customizable and doesn't include forced music.
            // Prioritizes the Everest controller if found, though you shouldn't be using both at once.

            StarJumpController vanillaController = Scene.Tracker.GetEntity<StarJumpController>();
            StarClimbGraphicsController everestController = Scene.Tracker.GetEntity<StarClimbGraphicsController>();

            Vector2 cameraPos = level.Camera.Position.Floor();
            VirtualRenderTarget blockFill = null;

            if (everestController != null)
                blockFill = StarClimbGraphicsController.BlockFill;
            else if (vanillaController != null)
                blockFill = vanillaController.BlockFill;

            if (blockFill != null) {
                Draw.SpriteBatch.Draw(
                    blockFill,
                    Position,
                    new Rectangle?(new Rectangle((int) (X - cameraPos.X), (int) (Y - cameraPos.Y), (int) Width, (int) Height)),
                    Color.White
                );
            }

            base_Render();
        }

        [MonoModIgnore]
        private extern int mod(int v, int count);

        [MonoModIgnore]
        private extern bool Open(float index, float v);
    }
}
