#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    static class patch_GFX {

        // Don't mind this ugly hack.
        public static bool LQ => Everest.Content.Get("lq") != null;

        public static extern void orig_LoadGame();
        public static void LoadGame() {
            orig_LoadGame();
            Everest.Events.GFX.LoadGame();
        }

        public static extern void orig_UnloadGame();
        public static void UnloadGame() {
            Everest.Events.GFX.UnloadGame();
            orig_UnloadGame();
        }

        public static extern void orig_LoadGui();
        public static void LoadGui() {
            if (!LQ) {
                orig_LoadGui();
            } else {
                GFX.Opening = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", "Opening"), Atlas.AtlasDataFormat.PackerNoAtlas);
                GFX.Gui = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", "GuiLQ"), Atlas.AtlasDataFormat.Packer);
                GFX.GuiSpriteBank = new SpriteBank(GFX.Gui, Path.Combine("Graphics", "SpritesGui.xml"));
                GFX.Journal = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", "Journal"), Atlas.AtlasDataFormat.Packer);
                GFX.Misc = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", "MiscLQ"), Atlas.AtlasDataFormat.PackerNoAtlas);
            }

            Everest.Events.GFX.LoadGui();
        }

        public static extern void orig_UnloadGui();
        public static void UnloadGui() {
            Everest.Events.GFX.UnloadGui();
            orig_UnloadGui();
        }

        public static extern void orig_LoadOverworld();
        public static void LoadOverworld() {
            orig_LoadOverworld();
            Everest.Events.GFX.LoadOverworld();
        }

        public static extern void orig_UnloadOverworld();
        public static void UnloadOverworld() {
            Everest.Events.GFX.UnloadOverworld();
            orig_UnloadOverworld();
        }

        public static extern void orig_LoadMountain();
        public static void LoadMountain() {
            if (!LQ) {
                orig_LoadMountain();
            } else {
                GFX.Mountain = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", "MountainLQ"), Atlas.AtlasDataFormat.PackerNoAtlas);
                GFX.Checkpoints = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", "CheckpointsLQ"), Atlas.AtlasDataFormat.Packer);
                GFX.MountainTerrainTextures = new VirtualTexture[3];
                GFX.MountainBuildingTextures = new VirtualTexture[3];
                GFX.MountainSkyboxTextures = new VirtualTexture[3];
                for (int i = 0; i < 3; i++) {
                    GFX.MountainTerrainTextures[i] = GFX.Mountain["mountain_" + i].Texture;
                    GFX.MountainBuildingTextures[i] = GFX.Mountain["buildings_" + i].Texture;
                    GFX.MountainSkyboxTextures[i] = GFX.Mountain["skybox_" + i].Texture;
                }
                GFX.MountainFogTexture = GFX.Mountain["fog"].Texture;
            }

            Everest.Events.GFX.LoadMountain();
        }

        public static extern void orig_UnloadMountain();
        public static void UnloadMountain() {
            Everest.Events.GFX.UnloadMountain();
            orig_UnloadMountain();
        }

        public static extern void orig_LoadOther();
        public static void LoadOther() {
            orig_LoadOther();
            Everest.Events.GFX.LoadOther();
        }

        public static extern void orig_UnloadOther();
        public static void UnloadOther() {
            Everest.Events.GFX.UnloadOther();
            orig_UnloadOther();
        }

        public static extern void orig_LoadPortraits();
        public static void LoadPortraits() {
            if (!LQ) {
                orig_LoadPortraits();
            } else {
                GFX.Portraits = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", "PortraitsLQ"), Atlas.AtlasDataFormat.PackerNoAtlas);
                GFX.PortraitsSpriteBank = new SpriteBank(GFX.Portraits, Path.Combine("Graphics", "PortraitsLQ.xml"));
            }

            Everest.Events.GFX.LoadPortraits();
        }

        public static extern void orig_UnloadPortraits();
        public static void UnloadPortraits() {
            Everest.Events.GFX.UnloadPortraits();
            orig_UnloadPortraits();
        }

        public static extern void orig_LoadData();
        public static void LoadData() {
            orig_LoadData();
            Everest.Events.GFX.LoadData();
        }

        public static extern void orig_UnloadData();
        public static void UnloadData() {
            Everest.Events.GFX.UnloadData();
            orig_UnloadData();
        }

        public static extern void orig_LoadEffects();
        public static void LoadEffects() {
            orig_LoadEffects();
            Everest.Events.GFX.LoadEffects();
        }

    }
}
