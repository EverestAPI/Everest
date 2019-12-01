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

        protected Ring customFog;
        protected Ring customFog2;
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
        }

        public extern void orig_Update();
        public new void Update() {
            orig_Update();
            string path = Path.Combine("Maps", SaveData.Instance?.LastArea.GetSID() ?? "").Replace('\\', '/');
            if (SaveData.Instance != null && Everest.Content.TryGet(path, out ModAsset asset)) {
                MapMeta meta;
                if (asset != null && (meta = asset.GetMeta<MapMeta>()) != null && meta.Mountain != null && !(string.IsNullOrEmpty(meta.Mountain.MountainModelDirectory) && string.IsNullOrEmpty(meta.Mountain.MountainTextureDirectory))) {
                    MountainResources resources = MTNExt.MountainMappings[path];
                    customFog.Rotate((0f - Engine.DeltaTime) * 0.01f);
                    customFog.TopColor = (customFog.BotColor = Color.Lerp((resources.MountainStates?[currState] ?? mountainStates[currState]).FogColor, (resources.MountainStates?[nextState] ?? mountainStates[nextState]).FogColor, easeState));
                    customFog2.Rotate((0f - Engine.DeltaTime) * 0.01f);
                    customFog2.TopColor = (customFog2.BotColor = Color.White * 0.3f * NearFogAlpha);
                }
            }
        }

        [MonoModIgnore]
        private extern void DrawBillboards(Matrix matrix, List<Component> billboards);

        public extern void orig_BeforeRender(Scene scene);
        public new void BeforeRender(Scene scene) {
            string path = Path.Combine("Maps", SaveData.Instance?.LastArea.GetSID() ?? "").Replace('\\', '/');
            string SIDToUse = SaveData.Instance?.LastArea.GetSID() ?? "";
            bool fadingIn = true;
            // Check if we're changing mountain models or textures
            // If so, we want to fade out and then back in
            if (!(SaveData.Instance?.LastArea.GetSID() ?? "").Equals(PreviousSID)) {
                string oldModelDir = "", oldTextureDir = "", newModelDir = "", newTextureDir = "";
                if (SaveData.Instance != null && Everest.Content.TryGet(path, out ModAsset asset1)) {
                    MapMeta meta;
                    if (asset1 != null && (meta = asset1.GetMeta<MapMeta>()) != null && meta.Mountain != null) {
                        newModelDir = meta.Mountain.MountainModelDirectory ?? "";
                        newTextureDir = meta.Mountain.MountainTextureDirectory ?? "";
                    }
                }
                string oldPath = Path.Combine("Maps", PreviousSID ?? "").Replace('\\', '/');
                if (SaveData.Instance != null && Everest.Content.TryGet(oldPath, out asset1)) {
                    MapMeta meta;
                    if (asset1 != null && (meta = asset1.GetMeta<MapMeta>()) != null && meta.Mountain != null) {
                        oldModelDir = meta.Mountain.MountainModelDirectory ?? "";
                        oldTextureDir = meta.Mountain.MountainTextureDirectory ?? "";
                    }
                }

                if (!oldModelDir.Equals(newModelDir) || !oldTextureDir.Equals(newTextureDir)) {
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
                if (asset != null && (meta = asset.GetMeta<MapMeta>()) != null && meta.Mountain != null && !(string.IsNullOrEmpty(meta.Mountain.MountainModelDirectory) && string.IsNullOrEmpty(meta.Mountain.MountainTextureDirectory))) {
                    MountainResources resources = MTNExt.MountainMappings[path];

                    ResetRenderTargets();
                    Quaternion rotation = Camera.Rotation;
                    if (ignoreCameraRotation) {
                        rotation = lastCameraRotation;
                    }
                    Matrix matrix = Matrix.CreatePerspectiveFieldOfView((float)Math.PI / 4f, (float)Engine.Width / (float)Engine.Height, 0.25f, 50f);
                    Matrix matrix2 = Matrix.CreateTranslation(-Camera.Position) * Matrix.CreateFromQuaternion(rotation);
                    Matrix matrix3 = matrix2 * matrix;
                    Forward = Vector3.Transform(Vector3.Forward, Camera.Rotation.Conjugated());
                    Engine.Graphics.GraphicsDevice.SetRenderTarget(buffer);

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

                    DrawBillboards(matrix3, scene.Tracker.GetComponents<Billboard>());
                    customFog2.Draw(matrix3, CullCRasterizer);

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
                    GaussianBlur.Blur((RenderTarget2D)buffer, blurA, blurB, 0.75f, clear: true, samples: GaussianBlur.Samples.Five);
                    Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * fade);

                    // Initialize new custom fog when we switch between maps
                    if (!(SIDToUse).Equals(PreviousSID) && resources.MountainFogTexture != null) {
                        customFog = new Ring(6f, -1f, 20f, 0f, 24, Color.White, resources.MountainFogTexture ?? MTN.MountainFogTexture);
                        customFog2 = new Ring(6f, -4f, 10f, 0f, 24, Color.White, resources.MountainFogTexture ?? MTN.MountainFogTexture);
                    }
                    PreviousSID = SIDToUse;
                    return;
                }
            }

            orig_BeforeRender(scene);
            Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * fade);
            PreviousSID = SIDToUse;
        }

    }
}
