#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Celeste.Mod.Meta;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

namespace Celeste {
    class patch_LevelLoader : LevelLoader {

        private static WeakReference<Thread> LastLoadingThread;

        public patch_LevelLoader(Session session, Vector2? startPosition = default(Vector2?))
            : base(session, startPosition) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(Session session, Vector2? startPosition = default(Vector2?));
        [MonoModConstructor]
        public void ctor(Session session, Vector2? startPosition = default(Vector2?)) {
            if (LastLoadingThread != null &&
                LastLoadingThread.TryGetTarget(out Thread lastThread) &&
                (lastThread?.IsAlive ?? false)) {
                lastThread?.Abort();
            }

            if (CoreModule.Settings.LazyLoading) {
                MainThreadHelper.Do(() => VirtualContentExt.UnloadOverworld());
            }

            // Vanilla TileToIndex mappings.
            SurfaceIndex.TileToIndex = new Dictionary<char, int> {
                { '1', 3 },
                { '3', 4 },
                { '4', 7 },
                { '5', 8 },
                { '6', 8 },
                { '7', 8 },
                { '8', 8 },
                { '9', 13 },
                { 'a', 8 },
                { 'b', 23 },
                { 'c', 8 },
                { 'd', 8 },
                { 'e', 8 },
                { 'f', 8 },
                { 'g', 8 },
                { 'G', 8 }, // Reflection alt - unassigned in vanilla.
                { 'h', 33 },
                { 'i', 4 },
                { 'j', 8 },
                { 'k', 3 },
                { 'l', 25 },
                { 'm', 44 },
                { 'n', 40 },
                { 'o', 43 }
            };

            AreaData area = AreaData.Get(session);
            MapMeta meta = area.GetMeta();
            string path;

            path = meta?.BackgroundTiles;
            if (string.IsNullOrEmpty(path))
                path = Path.Combine("Graphics", "BackgroundTiles.xml");
            GFX.BGAutotiler = new Autotiler(path);

            path = meta?.ForegroundTiles;
            if (string.IsNullOrEmpty(path))
                path = Path.Combine("Graphics", "ForegroundTiles.xml");
            GFX.FGAutotiler = new Autotiler(path);

            path = meta?.AnimatedTiles;
            if (string.IsNullOrEmpty(path))
                path = Path.Combine("Graphics", "AnimatedTiles.xml");
            GFX.AnimatedTilesBank = new AnimatedTilesBank();
            XmlElement animatedData = Calc.LoadContentXML(path)["Data"];
            foreach (XmlElement el in animatedData)
                if (el != null)
                    GFX.AnimatedTilesBank.Add(
                        el.Attr("name"),
                        el.AttrFloat("delay", 0f),
                        el.AttrVector2("posX", "posY", Vector2.Zero),
                        el.AttrVector2("origX", "origY", Vector2.Zero),
                        GFX.Game.GetAtlasSubtextures(el.Attr("path"))
                    );

            GFX.SpriteBank = new SpriteBank(GFX.Game, Path.Combine("Graphics", "Sprites.xml"));

            path = meta?.Sprites;
            if (!string.IsNullOrEmpty(path)) {
                SpriteBank bankOrig = GFX.SpriteBank;
                SpriteBank bankMod = new SpriteBank(GFX.Game, path);

                foreach (KeyValuePair<string, SpriteData> kvpBank in bankMod.SpriteData) {
                    string key = kvpBank.Key;
                    SpriteData valueMod = kvpBank.Value;

                    if (bankOrig.SpriteData.TryGetValue(key, out SpriteData valueOrig)) {
                        IDictionary animsOrig = valueOrig.Sprite.GetAnimations();
                        IDictionary animsMod = valueMod.Sprite.GetAnimations();
                        foreach (DictionaryEntry kvpAnim in animsMod) {
                            animsOrig[kvpAnim.Key] = kvpAnim.Value;
                        }

                        valueOrig.Sources.AddRange(valueMod.Sources);

                        // replay the starting animation to be sure it is referring to the new sprite.
                        valueOrig.Sprite.Stop();
                        if (valueMod.Sprite.CurrentAnimationID != "") {
                            valueOrig.Sprite.Play(valueMod.Sprite.CurrentAnimationID);
                        }
                    } else {
                        bankOrig.SpriteData[key] = valueMod;
                    }
                }
            }

            // This is done exactly once in the vanilla GFX.LoadData method.
            PlayerSprite.ClearFramesMetadata();
            PlayerSprite.CreateFramesMetadata("player");
            PlayerSprite.CreateFramesMetadata("player_no_backpack");
            PlayerSprite.CreateFramesMetadata("badeline");
            PlayerSprite.CreateFramesMetadata("player_badeline");
            PlayerSprite.CreateFramesMetadata("player_playback");

            path = meta?.Portraits;
            if (string.IsNullOrEmpty(path))
                path = Path.Combine("Graphics", "Portraits.xml");
            GFX.PortraitsSpriteBank = new SpriteBank(GFX.Portraits, path);

            orig_ctor(session, startPosition);

            LastLoadingThread = patch_RunThread.Current;
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchLevelLoaderThread] // ... except for manually manipulating the method via MonoModRules
        private extern void LoadingThread();

    }
}
