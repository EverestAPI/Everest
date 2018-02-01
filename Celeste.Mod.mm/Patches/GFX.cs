#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    static class patch_GFX {

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
            orig_LoadGui();
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
            orig_LoadMountain();
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
            orig_LoadPortraits();
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
