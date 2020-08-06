#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Monocle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using MonoMod;
using Celeste.Mod;
using Celeste.Mod.Meta;
using System.IO;
using System.Linq;

namespace Celeste {
    class patch_MountainModel : MountainModel {
        // A whole bunch of private fields need to be used in BeforeRender
        private bool ignoreCameraRotation;
        private Quaternion lastCameraRotation;
        private VirtualRenderTarget buffer;
        private int currState;
        private int nextState;
        private float easeState;
        private VirtualRenderTarget blurA;
        private VirtualRenderTarget blurB;
        private MountainState[] mountainStates;
        private Ring fog;
        private Ring fog2;

        private Ring starsky;
        private Ring starfog;
        private Ring stardots0;
        private Ring starstream0;
        private Ring starstream1;
        private Ring starstream2;

        private Vector3 starCenter;
        private float birdTimer;

        protected Ring customFog;
        protected Ring customFog2;

        private Ring customStarsky;
        private Ring customStarfog;
        private Ring customStardots0;
        private Ring customStarstream0;
        private Ring customStarstream1;
        private Ring customStarstream2;

        private MoonParticle3D vanillaMoonParticles;
        private MoonParticle3D customMoonParticles;

        // Used to check when we transition from a different area
        protected string PreviousSID;
        // How opaque the bg is when transitioning between models
        protected float fade = 0f;
        protected float fadeHoldCountdown = 0;

        public extern void orig_ctor();
        [MonoModConstructor]
        public void ctor() {
            orig_ctor();
            customFog = fog;
            customFog2 = fog2;

            customStarsky = starsky;
            customStarfog = starfog;
            customStardots0 = stardots0;
            customStarstream0 = starstream0;
            customStarstream1 = starstream1;
            customStarstream2 = starstream2;
        }

        public extern void orig_Update();
        public new void Update() {
            orig_Update();
            string path;
            try {
                path = Path.Combine("Maps", SaveData.Instance?.LastArea.GetSID() ?? "").Replace('\\', '/');
            } catch (ArgumentException) {
                path = "Maps";
            }
            if (SaveData.Instance != null && Everest.Content.TryGet(path, out ModAsset asset)) {
                MapMeta meta;
                if (asset != null && (meta = asset.GetMeta<MapMeta>()) != null && meta.Mountain != null && hasCustomSettings(meta)) {
                    MountainResources resources = MTNExt.MountainMappings[path];
                    customFog.Rotate((0f - Engine.DeltaTime) * 0.01f);
                    customFog.TopColor = (customFog.BotColor = Color.Lerp((resources.MountainStates?[currState] ?? mountainStates[currState]).FogColor, (resources.MountainStates?[nextState] ?? mountainStates[nextState]).FogColor, easeState));
                    customFog2.Rotate((0f - Engine.DeltaTime) * 0.01f);
                    customFog2.TopColor = (customFog2.BotColor = Color.White * 0.3f * NearFogAlpha);
                    customStarstream1.Rotate(Engine.DeltaTime * 0.01f);
                    customStarstream2.Rotate(Engine.DeltaTime * 0.02f);
                }
            }
        }

        [MonoModIgnore]
        private extern void DrawBillboards(Matrix matrix, List<Component> billboards);

        public extern void orig_BeforeRender(Scene scene);
        public new void BeforeRender(Scene scene) {
            if (vanillaMoonParticles == null) {
                vanillaMoonParticles = (Engine.Scene as Overworld)?.Entities.OfType<MoonParticle3D>().First();
            }

            string path;
            try {
                path = Path.Combine("Maps", SaveData.Instance?.LastArea.GetSID() ?? "").Replace('\\', '/');
            } catch (ArgumentException) {
                path = "Maps";
            }
            string SIDToUse = SaveData.Instance?.LastArea.GetSID() ?? "";
            bool fadingIn = true;
            // Check if we're changing any mountain parameter
            // If so, we want to fade out and then back in
            if (!(SaveData.Instance?.LastArea.GetSID() ?? "").Equals(PreviousSID)) {
                MapMetaMountain oldMountain = null;
                MapMetaMountain newMountain = null;
                if (SaveData.Instance != null && Everest.Content.TryGet(path, out ModAsset asset1)) {
                    MapMeta meta;
                    if (asset1 != null && (meta = asset1.GetMeta<MapMeta>()) != null && meta.Mountain != null) {
                        newMountain = meta.Mountain;
                    }
                }
                string oldPath;
                try {
                    path = Path.Combine("Maps", PreviousSID ?? "").Replace('\\', '/');
                } catch (ArgumentException) {
                    path = "Maps";
                }
                if (SaveData.Instance != null && Everest.Content.TryGet(oldPath, out asset1)) {
                    MapMeta meta;
                    if (asset1 != null && (meta = asset1.GetMeta<MapMeta>()) != null && meta.Mountain != null) {
                        oldMountain = meta.Mountain;
                    }
                }

                if (oldMountain?.MountainModelDirectory != newMountain?.MountainModelDirectory
                    || oldMountain?.MountainTextureDirectory != newMountain?.MountainTextureDirectory
                    || oldMountain?.StarFogColor != newMountain?.StarFogColor
                    || !arrayEqual(oldMountain?.StarStreamColors, newMountain?.StarStreamColors)
                    || !arrayEqual(oldMountain?.StarBeltColors1, newMountain?.StarBeltColors1)
                    || !arrayEqual(oldMountain?.StarBeltColors2, newMountain?.StarBeltColors2)) {

                    if (fade != 1f) {
                        SIDToUse = PreviousSID;
                        path = oldPath;
                        fade = Calc.Approach(fade, 1f, Engine.DeltaTime * 4f);
                        fadingIn = false;
                    } else {
                        // How long we want it to stay opaque before fading back in
                        fadeHoldCountdown = .3f;
                    }
                }
            }

            if (fadingIn && fade != 0f) {
                if (fadeHoldCountdown <= 0) {
                    fade = Calc.Approach(fade, 0f, Engine.DeltaTime * 4f);
                } else {
                    fadeHoldCountdown -= Engine.DeltaTime;
                }
            }

            if (SaveData.Instance != null && Everest.Content.TryGet(path, out ModAsset asset)) {
                MapMeta meta;
                if (asset != null && (meta = asset.GetMeta<MapMeta>()) != null && meta.Mountain != null && hasCustomSettings(meta)) {
                    MountainResources resources = MTNExt.MountainMappings[path];

                    ResetRenderTargets();
                    Quaternion rotation = Camera.Rotation;
                    if (ignoreCameraRotation) {
                        rotation = lastCameraRotation;
                    }
                    Matrix matrix = Matrix.CreatePerspectiveFieldOfView((float) Math.PI / 4f, (float) Engine.Width / (float) Engine.Height, 0.25f, 50f);
                    Matrix matrix2 = Matrix.CreateTranslation(-Camera.Position) * Matrix.CreateFromQuaternion(rotation);
                    Matrix matrix3 = matrix2 * matrix;
                    Forward = Vector3.Transform(Vector3.Forward, Camera.Rotation.Conjugated());
                    Engine.Graphics.GraphicsDevice.SetRenderTarget(buffer);

                    if (StarEase < 1f) {
                        Matrix matrix4 = Matrix.CreateTranslation(0f, 5f - Camera.Position.Y * 1.1f, 0f) * Matrix.CreateFromQuaternion(rotation) * matrix;

                        if (currState == nextState) {
                            (resources.MountainStates?[currState] ?? mountainStates[currState]).Skybox.Draw(matrix4, Color.White);
                        } else {
                            (resources.MountainStates?[currState] ?? mountainStates[currState]).Skybox.Draw(matrix4, Color.White);
                            (resources.MountainStates?[currState] ?? mountainStates[currState]).Skybox.Draw(matrix4, Color.White * easeState);
                        }
                        if (currState != nextState) {
                            GFX.FxMountain.Parameters["ease"].SetValue(easeState);
                            GFX.FxMountain.CurrentTechnique = GFX.FxMountain.Techniques["Easing"];
                        } else {
                            GFX.FxMountain.CurrentTechnique = GFX.FxMountain.Techniques["Single"];
                        }
                        Engine.Graphics.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                        Engine.Graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
                        Engine.Graphics.GraphicsDevice.RasterizerState = MountainRasterizer;
                        GFX.FxMountain.Parameters["WorldViewProj"].SetValue(matrix3);
                        GFX.FxMountain.Parameters["fog"].SetValue(customFog.TopColor.ToVector3());
                        Engine.Graphics.GraphicsDevice.Textures[0] = (resources.MountainStates?[currState] ?? mountainStates[currState]).TerrainTexture.Texture;
                        Engine.Graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
                        if (currState != nextState) {
                            Engine.Graphics.GraphicsDevice.Textures[1] = (resources.MountainStates?[nextState] ?? mountainStates[nextState]).TerrainTexture.Texture;
                            Engine.Graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
                        }
                        (resources.MountainTerrain ?? MTN.MountainTerrain).Draw(GFX.FxMountain);
                        GFX.FxMountain.Parameters["WorldViewProj"].SetValue(Matrix.CreateTranslation(CoreWallPosition) * matrix3);
                        (resources.MountainCoreWall ?? MTN.MountainCoreWall).Draw(GFX.FxMountain);
                        GFX.FxMountain.Parameters["WorldViewProj"].SetValue(matrix3);
                        Engine.Graphics.GraphicsDevice.Textures[0] = (resources.MountainStates?[currState] ?? mountainStates[currState]).BuildingsTexture.Texture;
                        Engine.Graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
                        if (currState != nextState) {
                            Engine.Graphics.GraphicsDevice.Textures[1] = (resources.MountainStates?[nextState] ?? mountainStates[nextState]).BuildingsTexture.Texture;
                            Engine.Graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
                        }
                        (resources.MountainBuildings ?? MTN.MountainBuildings).Draw(GFX.FxMountain);
                        customFog.Draw(matrix3);
                    }

                    if (StarEase > 0f) {
                        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null);
                        Draw.Rect(0f, 0f, buffer.Width, buffer.Height, Color.Black * Ease.CubeInOut(Calc.ClampedMap(StarEase, 0f, 0.6f)));
                        Draw.SpriteBatch.End();
                        Matrix matrix5 = Matrix.CreateTranslation(starCenter - Camera.Position) * Matrix.CreateFromQuaternion(rotation) * matrix;
                        float alpha = Calc.ClampedMap(StarEase, 0.8f, 1f);
                        customStarsky.Draw(matrix5, CullCCRasterizer, alpha);
                        customStarfog.Draw(matrix5, CullCCRasterizer, alpha);
                        customStardots0.Draw(matrix5, CullCCRasterizer, alpha);
                        customStarstream0.Draw(matrix5, CullCCRasterizer, alpha);
                        customStarstream1.Draw(matrix5, CullCCRasterizer, alpha);
                        customStarstream2.Draw(matrix5, CullCCRasterizer, alpha);
                        Engine.Graphics.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                        Engine.Graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
                        Engine.Graphics.GraphicsDevice.RasterizerState = CullCRasterizer;
                        Engine.Graphics.GraphicsDevice.Textures[0] = (resources.MountainMoonTexture ?? MTN.MountainMoonTexture).Texture;
                        Engine.Graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
                        GFX.FxMountain.CurrentTechnique = GFX.FxMountain.Techniques["Single"];
                        GFX.FxMountain.Parameters["WorldViewProj"].SetValue(matrix3);
                        GFX.FxMountain.Parameters["fog"].SetValue(fog.TopColor.ToVector3());
                        (resources.MountainMoon ?? MTN.MountainMoon).Draw(GFX.FxMountain);
                        float num = birdTimer * 0.2f;
                        Matrix matrix6 = Matrix.CreateScale(0.25f) * Matrix.CreateRotationZ((float) Math.Cos(num * 2f) * 0.5f) * Matrix.CreateRotationX(0.4f + (float) Math.Sin(num) * 0.05f) * Matrix.CreateRotationY(0f - num - (float) Math.PI / 2f) * Matrix.CreateTranslation((float) Math.Cos(num) * 2.2f, 31f + (float) Math.Sin(num * 2f) * 0.8f, (float) Math.Sin(num) * 2.2f);
                        GFX.FxMountain.Parameters["WorldViewProj"].SetValue(matrix6 * matrix3);
                        GFX.FxMountain.Parameters["fog"].SetValue(fog.TopColor.ToVector3());
                        (resources.MountainBird ?? MTN.MountainBird).Draw(GFX.FxMountain);
                    }

                    DrawBillboards(matrix3, scene.Tracker.GetComponents<Billboard>());

                    if (StarEase < 1f) {
                        customFog2.Draw(matrix3, CullCRasterizer);
                    }

                    if (DrawDebugPoints && DebugPoints.Count > 0) {
                        GFX.FxDebug.World = Matrix.Identity;
                        GFX.FxDebug.View = matrix2;
                        GFX.FxDebug.Projection = matrix;
                        GFX.FxDebug.TextureEnabled = false;
                        GFX.FxDebug.VertexColorEnabled = true;
                        VertexPositionColor[] array = DebugPoints.ToArray();
                        foreach (EffectPass pass in GFX.FxDebug.CurrentTechnique.Passes) {
                            pass.Apply();
                            Engine.Graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, array, 0, array.Length / 3);
                        }
                    }
                    GaussianBlur.Blur((RenderTarget2D) buffer, blurA, blurB, 0.75f, clear: true, samples: GaussianBlur.Samples.Five);

                    Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null);
                    Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * fade);
                    Draw.SpriteBatch.End();

                    // Initialize new custom fog and star belt when we switch between maps
                    if (!(SIDToUse).Equals(PreviousSID)) {
                        customFog = new Ring(6f, -1f, 20f, 0f, 24, Color.White, resources.MountainFogTexture ?? MTN.MountainFogTexture);
                        customFog2 = new Ring(6f, -4f, 10f, 0f, 24, Color.White, resources.MountainFogTexture ?? MTN.MountainFogTexture);
                        customStarsky = new Ring(18f, -18f, 20f, 0f, 24, Color.White, Color.Transparent, resources.MountainSpaceTexture ?? MTN.MountainStarSky);
                        customStarfog = new Ring(10f, -18f, 19.5f, 0f, 24, resources.StarFogColor ?? Calc.HexToColor("020915"), Color.Transparent, resources.MountainFogTexture ?? MTN.MountainFogTexture);
                        customStardots0 = new Ring(16f, -18f, 19f, 0f, 24, Color.White, Color.Transparent, resources.MountainSpaceStarsTexture ?? MTN.MountainStars, 4f);
                        customStarstream0 = new Ring(5f, -8f, 18.5f, 0.2f, 80, resources.StarStreamColors?[0] ?? Color.Black, resources.MountainStarStreamTexture ?? MTN.MountainStarStream);
                        customStarstream1 = new Ring(4f, -6f, 18f, 1f, 80, resources.StarStreamColors?[1] ?? Calc.HexToColor("9228e2") * 0.5f, resources.MountainStarStreamTexture ?? MTN.MountainStarStream);
                        customStarstream2 = new Ring(3f, -4f, 17.9f, 1.4f, 80, resources.StarStreamColors?[2] ?? Calc.HexToColor("30ffff") * 0.5f, resources.MountainStarStreamTexture ?? MTN.MountainStarStream);

                        if (Engine.Scene is Overworld thisOverworld) {
                            thisOverworld.Remove(customMoonParticles);
                            if (resources.StarBeltColors1 != null && resources.StarBeltColors2 != null) {
                                // there are custom moon particle colors. build the new particles and add them to the scene
                                thisOverworld.Remove(vanillaMoonParticles);
                                thisOverworld.Add(customMoonParticles = new patch_MoonParticle3D(this, new Vector3(0f, 31f, 0f), resources.StarBeltColors1, resources.StarBeltColors2));
                            } else {
                                // there are no more moon particle colors. restore the vanilla particles
                                customMoonParticles = null;
                                thisOverworld.Add(vanillaMoonParticles);
                            }
                        }
                    }
                    PreviousSID = SIDToUse;
                    return;
                }
            }

            if (customMoonParticles != null && Engine.Scene is Overworld overworld) {
                // revert back the moon particles to vanilla.
                overworld.Remove(customMoonParticles);
                customMoonParticles = null;
                overworld.Add(vanillaMoonParticles);
            }

            orig_BeforeRender(scene);

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null);
            Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * fade);
            Draw.SpriteBatch.End();

            PreviousSID = SIDToUse;
        }

        private static bool hasCustomSettings(MapMeta meta) {
            return !string.IsNullOrEmpty(meta.Mountain.MountainModelDirectory) || !string.IsNullOrEmpty(meta.Mountain.MountainTextureDirectory)
                || !string.IsNullOrEmpty(meta.Mountain.StarFogColor) || meta.Mountain.StarStreamColors != null
                || (meta.Mountain.StarBeltColors1 != null && meta.Mountain.StarBeltColors2 != null);
        }

        private bool arrayEqual(string[] array1, string[] array2) {
            if (array1 == null || array2 == null) {
                return array1 == array2;
            }
            return Enumerable.SequenceEqual(array1, array2);
        }
    }
}
