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
        private const int RayCount = 100;

        public static VirtualRenderTarget BlockFill;

        private static Ray[] rays = new Ray[RayCount];

        private VertexPositionColor[] vertices = new VertexPositionColor[600];
        private int vertexCount = 0;
        
        private Color rayColor;
        private Color wipeColor;
        
        private Level level;

        public StarClimbGraphicsController(EntityData data, Vector2 offset) {
            Tag = Tags.TransitionUpdate | Tags.FrozenUpdate;
            rayColor = Calc.HexToColor(data.Attr("fgColor", "a3ffff")) * 0.25f;
            wipeColor = Calc.HexToColor(data.Attr("bgColor", "293E4B"));
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            level = scene as Level;

            if (!DetectOtherController()) {
                InitBlockFill();
            }
            Add(new BeforeRenderHook(new Action(BeforeRender)));
        }

        public override void Update() {
            base.Update();
            UpdateBlockFill();
        }

        private bool DetectOtherController() {
            List<Entity> controllers = level.Tracker.GetEntities<StarClimbGraphicsController>();

            foreach (Entity control in controllers) {
                if (!(control is StarClimbGraphicsController other) || other == this)
                    continue;
                else
                    return true;
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
                if (rays[index1].Percent >= 1.0)
                    rays[index1].Reset();

                rays[index1].Percent += Engine.DeltaTime / rays[index1].Duration;
                rays[index1].Y += 8f * Engine.DeltaTime;

                Vector2 rayPosition = new Vector2(
                    mod(rays[index1].X - level.Camera.X * 0.9f, 320f + 160f) - 80f,
                    mod(rays[index1].Y - level.Camera.Y * 0.7f, 580f) - 200f
                );

                // Construct the ray
                float width = rays[index1].Width;
                float length = rays[index1].Length;
                Color rayLifeColor = rayColor * Ease.CubeInOut(Calc.YoYo(rays[index1].Percent));
                VertexPositionColor vert1 = new VertexPositionColor(new Vector3(rayPosition + rayAngleCompl * width + rayAngle * length, 0.0f), rayLifeColor);
                VertexPositionColor vert2 = new VertexPositionColor(new Vector3(rayPosition - rayAngleCompl * width, 0.0f), rayLifeColor);
                VertexPositionColor vert3 = new VertexPositionColor(new Vector3(rayPosition + rayAngleCompl * width, 0.0f), rayLifeColor);
                VertexPositionColor vert4 = new VertexPositionColor(new Vector3(rayPosition - rayAngleCompl * width - rayAngle * length, 0.0f), rayLifeColor);

                // Add ray tris
                vertices[verticeCount++] = vert1;
                vertices[verticeCount++] = vert2;
                vertices[verticeCount++] = vert3;
                vertices[verticeCount++] = vert2;
                vertices[verticeCount++] = vert3;
                vertices[verticeCount++] = vert4;
            }
            vertexCount = verticeCount;
        }

        private void BeforeRender() {
            if (BlockFill == null)
                BlockFill = VirtualContent.CreateRenderTarget("block-fill", 320, 180, false, true, 0);
            if (vertexCount <= 0)
                return;
            Engine.Graphics.GraphicsDevice.SetRenderTarget(BlockFill);
            Engine.Graphics.GraphicsDevice.Clear(wipeColor);
            GFX.DrawVertices(Matrix.Identity, vertices, vertexCount, null, null);
        }

        public override void Removed(Scene scene) {
            Dispose();
            base.Removed(scene);
        }

        public override void SceneEnd(Scene scene) {
            Dispose();
            base.SceneEnd(scene);
        }

        private void Dispose() {
            if (DetectOtherController())
                return;
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
                Percent = 0.0f;
                X = Calc.Random.NextFloat(320f + 160f);
                Y = Calc.Random.NextFloat(580f);
                Duration = 4.0f + Calc.Random.NextFloat() * 8.0f;
                Width = Calc.Random.Next(8, 80);
                Length = Calc.Random.Next(20, 200);
            }
        }
    }
}
