#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        // custom copies of the mountain objects, rendered instead of vanilla ones when the mountain has custom settings.
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
        internal string PreviousSID;
        // How opaque the bg is when transitioning between models
        protected float fade = 0f;
        protected float fadeHoldCountdown = 0;

        // fog colors used when switching between 2 states
        private Color previousFogColor = Color.White;
        private Color targetFogColor = Color.White;
        private float fogFade = 1f;
        private bool firstUpdate = true;

        // Private vars required for queued disposal.
        private VertexBuffer billboardVertices;
        private IndexBuffer billboardIndices;
        private VertexPositionColorTexture[] billboardInfo;

        private object _Billboard_QueuedLoadLock;
        private ValueTask<VertexBuffer> _Billboard_QueuedLoad;

        public extern void orig_ctor();
        [MonoModConstructor]
        public void ctor() {
            firstUpdate = true; // we need that for firstUpdate to actually get initialized to true.

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
                    // the mountain is custom!
                    if (firstUpdate || PreviousSID == (SaveData.Instance?.LastArea.GetSID() ?? "")) {
                        // we aren't fading out, so we can update the fog color.
                        MountainResources resources = MTNExt.MountainMappings[path];
                        Color fogColor = (resources.MountainStates?[nextState] ?? mountainStates[nextState]).FogColor;
                        if (firstUpdate || fade == 1f) {
                            // we faded to black, or just came back from a map with a custom mountain: snap the fog color.
                            targetFogColor = fogColor;
                            fogFade = 1f;
                        } else if (fogColor != targetFogColor) {
                            // the fog color changed! start fading from the current fog color to the new color.
                            previousFogColor = customFog.TopColor;
                            targetFogColor = fogColor;
                            fogFade = 0f;
                        }

                        // fade between previousFogColor and targetFogColor.
                        customFog.TopColor = customFog.BotColor = Color.Lerp(previousFogColor, targetFogColor, fogFade);
                        fogFade = Calc.Approach(fogFade, 1f, Engine.DeltaTime);
                    }

                    // refresh custom mountain objets (rotate the fog, etc)
                    customFog.Rotate((0f - Engine.DeltaTime) * 0.01f);
                    customFog2.Rotate((0f - Engine.DeltaTime) * 0.01f);
                    customFog2.TopColor = (customFog2.BotColor = Color.White * 0.3f * NearFogAlpha);
                    customStarstream1.Rotate(Engine.DeltaTime * 0.01f);
                    customStarstream2.Rotate(Engine.DeltaTime * 0.02f);
                    customStarfog.Rotate(-Engine.DeltaTime * 0.01f);
                }
            }

            firstUpdate = false;
        }

        [MonoModIgnore]
        private extern void DrawBillboards(Matrix matrix, List<Component> billboards);

        public extern void orig_BeforeRender(Scene scene);
        public new void BeforeRender(Scene scene) {
            if (vanillaMoonParticles == null) {
                // back up the vanilla particles.
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
            if (PreviousSID != null && !(SaveData.Instance?.LastArea.GetSID() ?? "").Equals(PreviousSID)) {
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
                    oldPath = Path.Combine("Maps", PreviousSID ?? "").Replace('\\', '/');
                } catch (ArgumentException) {
                    oldPath = "Maps";
                }
                if (SaveData.Instance != null && Everest.Content.TryGet(oldPath, out asset1)) {
                    MapMeta meta;
                    if (asset1 != null && (meta = asset1.GetMeta<MapMeta>()) != null && meta.Mountain != null) {
                        oldMountain = meta.Mountain;
                    }
                }

                if (oldMountain?.MountainModelDirectory != newMountain?.MountainModelDirectory
                    || oldMountain?.MountainTextureDirectory != newMountain?.MountainTextureDirectory
                    || (oldMountain?.FogColors == null) != (newMountain?.FogColors == null) // only fade to black if one end has custom fog colors and the other doesn't.
                    || oldMountain?.StarFogColor != newMountain?.StarFogColor
                    || (oldMountain?.ShowSnow ?? true) != (newMountain?.ShowSnow ?? true)
                    || !arrayEqual(oldMountain?.StarStreamColors, newMountain?.StarStreamColors)
                    || !arrayEqual(oldMountain?.StarBeltColors1, newMountain?.StarBeltColors1)
                    || !arrayEqual(oldMountain?.StarBeltColors2, newMountain?.StarBeltColors2)) {

                    if (fade != 1f) {
                        // fade out, and continue using the old mountain during the fadeout.
                        SIDToUse = PreviousSID;
                        path = oldPath;
                        fade = Calc.Approach(fade, 1f, Engine.DeltaTime * 4f);
                        fadingIn = false;
                    } else {
                        // start holding the black screen
                        fadeHoldCountdown = .3f;
                    }
                }
            }

            if (fadingIn && fade != 0f) {
                if (fadeHoldCountdown <= 0) {
                    // fade in
                    fade = Calc.Approach(fade, 0f, Engine.DeltaTime * 4f);
                } else {
                    // hold the black screen
                    fadeHoldCountdown -= Engine.DeltaTime;
                }
            }

            if (SaveData.Instance != null && Everest.Content.TryGet(path, out ModAsset asset)) {
                MapMeta meta;
                if (asset != null && (meta = asset.GetMeta<MapMeta>()) != null && meta.Mountain != null && hasCustomSettings(meta)) {
                    // there is a custom mountain! render it, similarly to vanilla.
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
                            (resources.MountainStates?[nextState] ?? mountainStates[nextState]).Skybox.Draw(matrix4, Color.White * easeState);
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
                        for (int i = 0; i < resources.MountainExtraModels.Count; i++) {
                            Engine.Graphics.GraphicsDevice.Textures[0] = (resources.MountainExtraModelTextures[i][currState] ?? mountainStates[currState].TerrainTexture).Texture;
                            Engine.Graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
                            if (currState != nextState) {
                                Engine.Graphics.GraphicsDevice.Textures[1] = (resources.MountainExtraModelTextures[i][nextState] ?? mountainStates[nextState].TerrainTexture).Texture;
                                Engine.Graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
                            }
                            resources.MountainExtraModels[i].Draw(GFX.FxMountain);
                        }
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

                    // render the fade to black.
                    Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null);
                    Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * fade);
                    Draw.SpriteBatch.End();

                    // Initialize new custom fog and star belt when we switch between maps
                    if (!(SIDToUse).Equals(PreviousSID)) {
                        // save values to make transition smoother.
                        Color fogColorBeforeReload = customFog?.TopColor ?? Color.White;
                        float spinBeforeReload = customFog.Verts[1].TextureCoordinate.X;

                        // build new objects with custom textures.
                        customFog = new Ring(6f, -1f, 20f, 0f, 24, Color.White, resources.MountainFogTexture ?? MTN.MountainFogTexture);
                        customFog2 = new Ring(6f, -4f, 10f, 0f, 24, Color.White, resources.MountainFogTexture ?? MTN.MountainFogTexture);
                        customStarsky = new Ring(18f, -18f, 20f, 0f, 24, Color.White, Color.Transparent, resources.MountainSpaceTexture ?? MTN.MountainStarSky);
                        customStarfog = new Ring(10f, -18f, 19.5f, 0f, 24, resources.StarFogColor ?? Calc.HexToColor("020915"), Color.Transparent, resources.MountainFogTexture ?? MTN.MountainFogTexture);
                        customStardots0 = new Ring(16f, -18f, 19f, 0f, 24, Color.White, Color.Transparent, resources.MountainSpaceStarsTexture ?? MTN.MountainStars, 4f);
                        customStarstream0 = new Ring(5f, -8f, 18.5f, 0.2f, 80, resources.StarStreamColors?[0] ?? Color.Black, resources.MountainStarStreamTexture ?? MTN.MountainStarStream);
                        customStarstream1 = new Ring(4f, -6f, 18f, 1f, 80, resources.StarStreamColors?[1] ?? Calc.HexToColor("9228e2") * 0.5f, resources.MountainStarStreamTexture ?? MTN.MountainStarStream);
                        customStarstream2 = new Ring(3f, -4f, 17.9f, 1.4f, 80, resources.StarStreamColors?[2] ?? Calc.HexToColor("30ffff") * 0.5f, resources.MountainStarStreamTexture ?? MTN.MountainStarStream);

                        // restore values saved earlier.
                        customFog.TopColor = customFog.BotColor = fogColorBeforeReload;
                        customFog.Rotate(spinBeforeReload);
                        customFog2.Rotate(spinBeforeReload);

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

            // if we are here, it means we don't have a custom mountain.

            if (customMoonParticles != null && Engine.Scene is Overworld overworld) {
                // revert back the moon particles to vanilla.
                overworld.Remove(customMoonParticles);
                customMoonParticles = null;
                overworld.Add(vanillaMoonParticles);
            }

            // run vanilla code.
            orig_BeforeRender(scene);

            // render the fade to black.
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null);
            Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * fade);
            Draw.SpriteBatch.End();

            PreviousSID = SIDToUse;
        }

        [MonoModReplace]
        public new void DisposeBillboardBuffers() {
            if (!MainThreadHelper.IsMainThread && (
                    (billboardVertices != null && !billboardVertices.IsDisposed) ||
                    (billboardIndices != null && !billboardIndices.IsDisposed)
                )) {
                // This is a disposal, there's no need to wait for this to be disposed,
                // let following reset calls realloc even before the olds get disposed.
                VertexBuffer billboardVerticesOld = billboardVertices;
                IndexBuffer billboardIndicesOld = billboardIndices;
                billboardVertices = null;
                billboardIndices = null;
                MainThreadHelper.Schedule(() => {
                    if (billboardVerticesOld != null && !billboardVerticesOld.IsDisposed)
                        billboardVerticesOld.Dispose();
                    if (billboardIndicesOld != null && !billboardIndicesOld.IsDisposed)
                        billboardIndicesOld.Dispose();
                });
                return;
            }

            if (billboardVertices != null && !billboardVertices.IsDisposed)
                billboardVertices.Dispose();
            if (billboardIndices != null && !billboardIndices.IsDisposed)
                billboardIndices.Dispose();
        }

        public extern void orig_ResetBillboardBuffers();
        public new void ResetBillboardBuffers() {
            // Checking for IsDisposed on other threads should be fine...
            if (billboardVertices != null && !billboardIndices.IsDisposed && !billboardIndices.GraphicsDevice.IsDisposed &&
                billboardVertices != null && !billboardVertices.IsDisposed && !billboardVertices.GraphicsDevice.IsDisposed &&
                billboardInfo.Length <= billboardVertices.VertexCount)
                return;

            // Handle already queued loads appropriately.
            object queuedLoadLock = _Billboard_QueuedLoadLock;
            if (queuedLoadLock != null) {
                lock (queuedLoadLock) {
                    // Queued task finished just in time.
                    if (_Billboard_QueuedLoadLock == null)
                        return;

                    // If we still can, cancel the queued load, then proceed with lazy-loading.
                    if (MainThreadHelper.IsMainThread)
                        _Billboard_QueuedLoadLock = null;
                }

                if (!MainThreadHelper.IsMainThread) {
                    // Otherwise wait for it to get loaded, don't reload twice. (Don't wait locked!)
                    _ = _Billboard_QueuedLoad.Result;
                    return;
                }
            }

            if (!(CoreModule.Settings.ThreadedGL ?? Everest.Flags.PreferThreadedGL) && !MainThreadHelper.IsMainThread && queuedLoadLock == null) {
                // Let's queue a reload onto the main thread and call it a day.
                lock (queuedLoadLock = new object()) {
                    _Billboard_QueuedLoadLock = queuedLoadLock;
                    _Billboard_QueuedLoad = MainThreadHelper.Schedule(() => {
                        lock (queuedLoadLock) {
                            if (_Billboard_QueuedLoadLock == null)
                                return billboardVertices;
                            // Force-reload as we already returned true on the other thread.
                            if (billboardVertices != null && !billboardVertices.IsDisposed)
                                billboardVertices.Dispose();
                            if (billboardIndices != null && !billboardIndices.IsDisposed)
                                billboardIndices.Dispose();
                            // NOTE: If something dares to change verts on the fly, make it wait on any existing tasks, then make it force-reload.
                            // Let's rely on the original code for now.
                            orig_ResetBillboardBuffers();
                            _Billboard_QueuedLoadLock = null;
                            return billboardVertices;
                        }
                    });
                }
                return;
            }

            orig_ResetBillboardBuffers();
        }

        private static bool hasCustomSettings(MapMeta meta) {
            return !string.IsNullOrEmpty(meta.Mountain.MountainModelDirectory) || !string.IsNullOrEmpty(meta.Mountain.MountainTextureDirectory)
                || meta.Mountain.FogColors != null || !string.IsNullOrEmpty(meta.Mountain.StarFogColor) || meta.Mountain.StarStreamColors != null
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
