#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Monocle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_MTN {
        // We don't need an Unload() because the vanilla MTN disposes with the Atlas we use

        public static extern void orig_UnloadData();
        public static void UnloadData() {
            if (MTN.DataLoaded) {
                foreach (KeyValuePair<string, MountainResources> kvp in MTNExt.MountainMappings) {
                    kvp.Value.MountainTerrain?.Dispose();
                    kvp.Value.MountainTerrain = null;
                    kvp.Value.MountainBuildings?.Dispose();
                    kvp.Value.MountainBuildings = null;
                    kvp.Value.MountainCoreWall?.Dispose();
                    kvp.Value.MountainCoreWall = null;
                }
            }
            orig_UnloadData();
        }

    }

    public class MountainResources {
        public ObjModel MountainTerrain;

        public ObjModel MountainBuildings;

        public ObjModel MountainCoreWall;

        public VirtualTexture[] MountainTerrainTextures;

        public VirtualTexture[] MountainBuildingTextures;

        public VirtualTexture[] MountainSkyboxTextures;

        public VirtualTexture MountainFogTexture;

        public MountainState[] MountainStates = new MountainState[4];
    }

    public static class MTNExt {
        /// <summary>
        /// Maps the key to the ModAsset of the map in Everest.Content to the MountainResources for it
        /// </summary>
        public static Dictionary<string, MountainResources> MountainMappings = new Dictionary<string, MountainResources>();

        public static bool ModsLoaded { get; private set; }
        public static bool ModsDataLoaded { get; private set; }

        /// <summary>
        /// Load the custom mountain models for mods.
        /// </summary>
        public static void LoadModData() {
            if (!ModsDataLoaded) {
                Stopwatch stopwatch = Stopwatch.StartNew();
                lock (Everest.Content.Map) {
                    foreach (KeyValuePair<string, ModAsset> kvp in Everest.Content.Map) {
                        MapMeta meta;
                        // Check if the meta for this asset exists and if it has a MountainModelDirectory specified
                        if (kvp.Value != null && (meta = kvp.Value.GetMeta<MapMeta>()) != null && meta.Mountain != null && !string.IsNullOrEmpty(meta.Mountain.MountainModelDirectory)) {
                            // Create the mountain resources for this map if they don't exist already
                            if (!MountainMappings.TryGetValue(kvp.Key, out MountainResources resources)) {
                                resources = new MountainResources();
                                MountainMappings.Add(kvp.Key, resources);
                            }

                            if (Everest.Content.TryGet(Path.Combine(meta.Mountain.MountainModelDirectory, "mountain"), out ModAsset mountain)) {
                                resources.MountainTerrain = ObjModelExt.CreateFromStream(mountain.Stream, Path.Combine(meta.Mountain.MountainModelDirectory, "mountain.obj"));
                            }

                            if (Everest.Content.TryGet(Path.Combine(meta.Mountain.MountainModelDirectory, "buildings"), out ModAsset buildings)) {
                                resources.MountainBuildings = ObjModelExt.CreateFromStream(buildings.Stream, Path.Combine(meta.Mountain.MountainModelDirectory, "buildings.obj"));
                            }

                            if (Everest.Content.TryGet(Path.Combine(meta.Mountain.MountainModelDirectory, "mountain_wall"), out ModAsset coreWall)) {
                                resources.MountainCoreWall = ObjModelExt.CreateFromStream(coreWall.Stream, Path.Combine(meta.Mountain.MountainModelDirectory, "mountain_wall.obj"));
                            }

                        }
                    }
                }
                Console.WriteLine(" - MODDED MTN DATA LOAD: " + stopwatch.ElapsedMilliseconds + "ms");
            }

            ModsDataLoaded = true;
        }
        /// <summary>
        /// Load the custom mountain textures for mods.
        /// </summary>
        public static void LoadMod() {
            if (!ModsLoaded) {
                Stopwatch stopwatch = Stopwatch.StartNew();
                lock (Everest.Content.Map) {
                    foreach (KeyValuePair<string, ModAsset> kvp in Everest.Content.Map) {
                        MapMeta meta;
                        // Check if the meta for this asset exists and if it has a MountainTextureDirectory specified
                        if (kvp.Value != null && (meta = kvp.Value.GetMeta<MapMeta>()) != null && meta.Mountain != null && !string.IsNullOrEmpty(meta.Mountain.MountainTextureDirectory)) {
                            // Create the mountain resources for this map if they don't exist already
                            if (!MountainMappings.TryGetValue(kvp.Key, out MountainResources resources)) {
                                resources = new MountainResources();
                                MountainMappings.Add(kvp.Key, resources);
                            }

                            resources.MountainTerrainTextures = new VirtualTexture[3];
                            resources.MountainBuildingTextures = new VirtualTexture[3];
                            resources.MountainSkyboxTextures = new VirtualTexture[3];
                            for (int i = 0; i < 3; i++) {
                                if (MTN.Mountain.Has(Path.Combine(meta.Mountain.MountainTextureDirectory, "skybox_" + i).Replace('\\', '/'))) {
                                    resources.MountainSkyboxTextures[i] = MTN.Mountain[Path.Combine(meta.Mountain.MountainTextureDirectory, "skybox_" + i).Replace('\\', '/')].Texture;
                                }
                                if (MTN.Mountain.Has(Path.Combine(meta.Mountain.MountainTextureDirectory, "mountain_" + i).Replace('\\', '/'))) {
                                    resources.MountainTerrainTextures[i] = MTN.Mountain[Path.Combine(meta.Mountain.MountainTextureDirectory, "mountain_" + i).Replace('\\', '/')].Texture;
                                }
                                if (MTN.Mountain.Has(Path.Combine(meta.Mountain.MountainTextureDirectory, "buildings_" + i).Replace('\\', '/'))) {
                                    resources.MountainBuildingTextures[i] = MTN.Mountain[Path.Combine(meta.Mountain.MountainTextureDirectory, "buildings_" + i).Replace('\\', '/')].Texture;
                                }
                            }
                            if (MTN.Mountain.Has(Path.Combine(meta.Mountain.MountainTextureDirectory, "fog").Replace('\\', '/'))) {
                                resources.MountainFogTexture = MTN.Mountain[Path.Combine(meta.Mountain.MountainTextureDirectory, "fog").Replace('\\', '/')].Texture;
                            }

                            // Use the default textures if no custom ones were loaded
                            resources.MountainStates[0] = new MountainState(resources.MountainTerrainTextures[0] ?? MTN.MountainTerrainTextures[0], resources.MountainBuildingTextures[0] ?? MTN.MountainBuildingTextures[0], resources.MountainSkyboxTextures[0] ?? MTN.MountainSkyboxTextures[0], Calc.HexToColor("010817"));
                            resources.MountainStates[1] = new MountainState(resources.MountainTerrainTextures[1] ?? MTN.MountainTerrainTextures[1], resources.MountainBuildingTextures[1] ?? MTN.MountainBuildingTextures[1], resources.MountainSkyboxTextures[1] ?? MTN.MountainSkyboxTextures[1], Calc.HexToColor("13203E"));
                            resources.MountainStates[2] = new MountainState(resources.MountainTerrainTextures[2] ?? MTN.MountainTerrainTextures[2], resources.MountainBuildingTextures[2] ?? MTN.MountainBuildingTextures[2], resources.MountainSkyboxTextures[2] ?? MTN.MountainSkyboxTextures[2], Calc.HexToColor("281A35"));
                            resources.MountainStates[3] = new MountainState(resources.MountainTerrainTextures[0] ?? MTN.MountainTerrainTextures[0], resources.MountainBuildingTextures[0] ?? MTN.MountainBuildingTextures[0], resources.MountainSkyboxTextures[0] ?? MTN.MountainSkyboxTextures[0], Calc.HexToColor("010817"));
                        }
                    }
                }
                Console.WriteLine(" - MODDED MTN LOAD: " + stopwatch.ElapsedMilliseconds + "ms");
            }
            ModsLoaded = true;
        }
    }
}
