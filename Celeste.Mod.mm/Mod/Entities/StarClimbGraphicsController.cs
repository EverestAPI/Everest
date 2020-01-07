using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;

// Decompile lifted from dotPeek, blame spaghetti on that. --GreyMaria

namespace Celeste.Mod.Entities
{
    [Tracked]
    [CustomEntity("everest/starClimbGraphicsController")]
    public class StarClimbGraphicsController : Entity
    {
        private VertexPositionColor[] vertices = new VertexPositionColor[600];
        private int vertexCount = 0;
        private Color rayColor; // = Calc.HexToColor("a3ffff") * 0.25f;
        private Color wipeColor;
        private StarClimbGraphicsController.Ray[] rays = new StarClimbGraphicsController.Ray[100];
        private Level level;
        private Random random;
        public VirtualRenderTarget BlockFill;
        private const int RayCount = 100;

        public StarClimbGraphicsController(EntityData data, Vector2 offset)
        {
            this.rayColor = Calc.HexToColor(data.Attr("fgColor", "a3ffff")) * 0.25f;
            this.wipeColor = Calc.HexToColor(data.Attr("bgColor", "293E4B"));
            this.InitBlockFill();
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            this.level = this.SceneAs<Level>();
            this.random = new Random(666);
            this.Add((Component)new BeforeRenderHook(new Action(this.BeforeRender)));
        }

        public override void Update()
        {
            base.Update();
            this.UpdateBlockFill();
        }

        private void InitBlockFill()
        {
            for (int index = 0; index < this.rays.Length; ++index)
            {
                this.rays[index].Reset();
                this.rays[index].Percent = Calc.Random.NextFloat();
            }
        }

        private void UpdateBlockFill()
        {
            Level scene = this.Scene as Level;
            Vector2 vector = Calc.AngleToVector(-1.670796f, 1f);
            Vector2 vector2_1 = new Vector2(-vector.Y, vector.X);
            int num1 = 0;
            for (int index1 = 0; index1 < this.rays.Length; ++index1)
            {
                if ((double)this.rays[index1].Percent >= 1.0)
                    this.rays[index1].Reset();
                this.rays[index1].Percent += Engine.DeltaTime / this.rays[index1].Duration;
                this.rays[index1].Y += 8f * Engine.DeltaTime;
                float percent = this.rays[index1].Percent;
                float num2 = this.mod(this.rays[index1].X - scene.Camera.X * 0.9f, 320f);
                float num3 = this.mod(this.rays[index1].Y - scene.Camera.Y * 0.7f, 580f) - 200f;
                float width = this.rays[index1].Width;
                float length = this.rays[index1].Length;
                Vector2 vector2_2 = new Vector2((float)(int)num2, (float)(int)num3);
                Color color = this.rayColor * Ease.CubeInOut(Calc.YoYo(percent));
                VertexPositionColor vertexPositionColor1 = new VertexPositionColor(new Vector3(vector2_2 + vector2_1 * width + vector * length, 0.0f), color);
                VertexPositionColor vertexPositionColor2 = new VertexPositionColor(new Vector3(vector2_2 - vector2_1 * width, 0.0f), color);
                VertexPositionColor vertexPositionColor3 = new VertexPositionColor(new Vector3(vector2_2 + vector2_1 * width, 0.0f), color);
                VertexPositionColor vertexPositionColor4 = new VertexPositionColor(new Vector3(vector2_2 - vector2_1 * width - vector * length, 0.0f), color);
                VertexPositionColor[] vertices1 = this.vertices;
                int index2 = num1;
                int num4 = index2 + 1;
                VertexPositionColor vertexPositionColor5 = vertexPositionColor1;
                vertices1[index2] = vertexPositionColor5;
                VertexPositionColor[] vertices2 = this.vertices;
                int index3 = num4;
                int num5 = index3 + 1;
                VertexPositionColor vertexPositionColor6 = vertexPositionColor2;
                vertices2[index3] = vertexPositionColor6;
                VertexPositionColor[] vertices3 = this.vertices;
                int index4 = num5;
                int num6 = index4 + 1;
                VertexPositionColor vertexPositionColor7 = vertexPositionColor3;
                vertices3[index4] = vertexPositionColor7;
                VertexPositionColor[] vertices4 = this.vertices;
                int index5 = num6;
                int num7 = index5 + 1;
                VertexPositionColor vertexPositionColor8 = vertexPositionColor2;
                vertices4[index5] = vertexPositionColor8;
                VertexPositionColor[] vertices5 = this.vertices;
                int index6 = num7;
                int num8 = index6 + 1;
                VertexPositionColor vertexPositionColor9 = vertexPositionColor3;
                vertices5[index6] = vertexPositionColor9;
                VertexPositionColor[] vertices6 = this.vertices;
                int index7 = num8;
                num1 = index7 + 1;
                VertexPositionColor vertexPositionColor10 = vertexPositionColor4;
                vertices6[index7] = vertexPositionColor10;
            }
            this.vertexCount = num1;
        }

        private void BeforeRender()
        {
            if (this.BlockFill == null)
                this.BlockFill = VirtualContent.CreateRenderTarget("block-fill", 320, 180, false, true, 0);
            if (this.vertexCount <= 0)
                return;
            Engine.Graphics.GraphicsDevice.SetRenderTarget((RenderTarget2D)this.BlockFill);
            Engine.Graphics.GraphicsDevice.Clear(wipeColor);
            GFX.DrawVertices<VertexPositionColor>(Matrix.Identity, this.vertices, this.vertexCount, (Effect)null, (BlendState)null);
        }

        public override void Removed(Scene scene)
        {
            this.Dispose();
            base.Removed(scene);
        }

        public override void SceneEnd(Scene scene)
        {
            this.Dispose();
            base.SceneEnd(scene);
        }

        private void Dispose()
        {
            if (this.BlockFill != null)
                this.BlockFill.Dispose();
            this.BlockFill = (VirtualRenderTarget)null;
        }

        private float mod(float x, float m)
        {
            return (x % m + m) % m;
        }

        private struct Ray
        {
            public float X;
            public float Y;
            public float Percent;
            public float Duration;
            public float Width;
            public float Length;

            public void Reset()
            {
                this.Percent = 0.0f;
                this.X = Calc.Random.NextFloat(320f);
                this.Y = Calc.Random.NextFloat(580f);
                this.Duration = (float)(4.0 + (double)Calc.Random.NextFloat() * 8.0);
                this.Width = (float)Calc.Random.Next(8, 80);
                this.Length = (float)Calc.Random.Next(20, 200);
            }
        }
    }
}
