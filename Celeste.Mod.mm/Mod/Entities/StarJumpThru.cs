using FMOD;
using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/starJumpThru")]
    [Tracked]
    public class StarJumpThru : JumpThru {

        private bool hasRail;

        public StarJumpThru(EntityData data, Vector2 offset) 
            : base(data.Position + offset, data.Width, true) {
            hasRail = data.Bool("hasRail", true);

            Depth = -60;
            SurfaceSoundIndex = 32;
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            List<MTexture> railsL = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/leftrailing");
            List<MTexture> rails = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/railing");
            List<MTexture> railsR = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/rightrailing");
            List<MTexture> edgesH = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/edgeH");
            List<MTexture> corners = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/corner");

            for (int i = 0; i < Width; i += 8) {
                Image edge = new Image(edgesH[0]);
                edge.CenterOrigin();
                edge.Position = new Vector2(i + 4, 4f);
                Add(edge);
            }

            for (int i = 8; i < Width - 8f; i += 8) {
                Image edge = new Image(Calc.Random.Choose(edgesH));
                edge.CenterOrigin();
                edge.Position = new Vector2(i + 4, 6f);
                Add(edge);

                if (hasRail) {
                    Image rail = new Image(rails[mod((int) (X + i) / 8, rails.Count)]);
                    rail.Position = new Vector2(i, -8f);
                    Add(rail);
                }
            }

            Image img;
            img = new Image(Calc.Random.Choose(corners));
            img.Scale.X = -1f;
            if (hasRail) {
                Image rail = new Image(railsL[mod((int) X / 8, railsL.Count)]);
                rail.Position = new Vector2(0f, -8f);
                Add(rail);
            }
            img.CenterOrigin();
            img.Position = new Vector2(4f, 6f);
            Add(img);


            img = new Image(Calc.Random.Choose(corners));
            if (hasRail) {
                Image rail = new Image(railsR[mod((int) (X + Width) / 8 - 1, railsR.Count)]);
                rail.Position = new Vector2(Width - 8f, -8f);
                Add(rail);
            }
            img.CenterOrigin();
            img.Position = new Vector2(Width - 4f, 6f);
            Add(img);
        }

        private int mod(int x, int m) {
            return (x % m + m) % m;
        }

        public override void Render() {
            Draw.Line(TopLeft, TopRight, Color.White);
            Draw.Line(TopLeft + Vector2.UnitX, new Vector2(Left + 1f, Top + 2f), Color.White);
            Draw.Line(TopRight, new Vector2(Right, Top + 2f), Color.White);
            base.Render();
        }

    }
}
