using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections.Generic;

// Decompile lifted from dotPeek, blame spaghetti on that. --GreyMaria

namespace Celeste.Mod.Entities {
    [Tracked]
    [CustomEntity("everest/starClimbGraphicsController")]
    public class StarClimbGraphicsController : Entity {
        private VertexPositionColor[] vertices = new VertexPositionColor[600];
        private int vertexCount = 0;
        private Color rayColor;
        private Color wipeColor;
        private static Ray[] rays = new Ray[100];
        private Level level;
        private static Random random;
        public static VirtualRenderTarget BlockFill;
        private const int RayCount = 100;

        public StarClimbGraphicsController(EntityData data, Vector2 offset) {
            this.Tag = (Tags.TransitionUpdate | Tags.FrozenUpdate);
            this.rayColor = Calc.HexToColor(data.Attr("fgColor", "a3ffff")) * 0.25f;
            this.wipeColor = Calc.HexToColor(data.Attr("bgColor", "293E4B"));
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            level = scene as Level;

            if (!DetectOtherController()) {
                this.InitBlockFill();
                random = new Random(666);
            }
            this.Add(new BeforeRenderHook(new Action(this.BeforeRender)));
        }

        public override void Update() {
            base.Update();
            this.UpdateBlockFill();
        }

        private bool DetectOtherController() {
            List<Entity> controllers = level.Tracker.GetEntities<StarClimbGraphicsController>();

            foreach (Entity control in controllers) {
                StarClimbGraphicsController other = control as StarClimbGraphicsController;
                if (other == null || other == this) continue;
                else return true;
            }
            return false;
        }

        private void InitBlockFill() {
            for (int index = 0; index < rays.Length; ++index) {
                rays[index].Reset();
                rays[index].Percent = Calc.Random.NextFloat();
            }
        }

        private void UpdateBlockFill() {
            Vector2 rayAngle = Calc.AngleToVector(-1.670796f, 1f);
            Vector2 rayAngleCompl = new Vector2(-rayAngle.Y, rayAngle.X);
            int verticeCount = 0;

            for (int index1 = 0; index1 < rays.Length; ++index1) {
                // Ray lifetime and expiry
                if ((double) rays[index1].Percent >= 1.0)
                    rays[index1].Reset();

                rays[index1].Percent += Engine.DeltaTime / rays[index1].Duration;
                rays[index1].Y += 8f * Engine.DeltaTime;

                Vector2 rayPosition = new Vector2(
                    mod(rays[index1].X - this.level.Camera.X * 0.9f, 320f + 160f) - 80f,
                    mod(rays[index1].Y - this.level.Camera.Y * 0.7f, 580f) - 200f
                );

                // Construct the ray
                float width = rays[index1].Width;
                float length = rays[index1].Length;
                Color rayLifeColor = this.rayColor * Ease.CubeInOut(Calc.YoYo(rays[index1].Percent));
                VertexPositionColor vert1 = new VertexPositionColor(new Vector3(rayPosition + rayAngleCompl * width + rayAngle * length, 0.0f), rayLifeColor);
                VertexPositionColor vert2 = new VertexPositionColor(new Vector3(rayPosition - rayAngleCompl * width, 0.0f), rayLifeColor);
                VertexPositionColor vert3 = new VertexPositionColor(new Vector3(rayPosition + rayAngleCompl * width, 0.0f), rayLifeColor);
                VertexPositionColor vert4 = new VertexPositionColor(new Vector3(rayPosition - rayAngleCompl * width - rayAngle * length, 0.0f), rayLifeColor);

                // Add ray tris
                this.vertices[verticeCount++] = vert1;
                this.vertices[verticeCount++] = vert2;
                this.vertices[verticeCount++] = vert3;
                this.vertices[verticeCount++] = vert2;
                this.vertices[verticeCount++] = vert3;
                this.vertices[verticeCount++] = vert4;
            }
            this.vertexCount = verticeCount;
        }

        private void BeforeRender() {
            if (BlockFill == null)
                BlockFill = VirtualContent.CreateRenderTarget("block-fill", 320, 180, false, true, 0);
            if (this.vertexCount <= 0)
                return;
            Engine.Graphics.GraphicsDevice.SetRenderTarget((RenderTarget2D) BlockFill);
            Engine.Graphics.GraphicsDevice.Clear(wipeColor);
            GFX.DrawVertices<VertexPositionColor>(Matrix.Identity, this.vertices, this.vertexCount, (Effect) null, (BlendState) null);
        }

        public override void Removed(Scene scene) {
            this.Dispose();
            base.Removed(scene);
        }

        public override void SceneEnd(Scene scene) {
            this.Dispose();
            base.SceneEnd(scene);
        }

        private void Dispose() {
            if (DetectOtherController()) return;
            if (BlockFill != null)
                BlockFill.Dispose();
            BlockFill = null;
        }

        private static float mod(float x, float m) {
            return (x % m + m) % m;
        }

        private struct Ray {
            public float X;
            public float Y;
            public float Percent;
            public float Duration;
            public float Width;
            public float Length;

            public void Reset() {
                this.Percent = 0.0f;
                this.X = Calc.Random.NextFloat(320f + 160f);
                this.Y = Calc.Random.NextFloat(580f);
                this.Duration = 4.0f + Calc.Random.NextFloat() * 8.0f;
                this.Width = (float) Calc.Random.Next(8, 80);
                this.Length = (float) Calc.Random.Next(20, 200);
            }
        }
    }
}
