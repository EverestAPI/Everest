#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

namespace Celeste {
    class patch_LevelLoader : LevelLoader {

        private bool started;
        private Session session;
        public bool Loaded { get; private set; }

        private static WeakReference<Thread> LastLoadingThread;

        public patch_LevelLoader(Session session, Vector2? startPosition = default)
            : base(session, startPosition) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [PatchLevelLoaderOrigCtor]
        public extern void orig_ctor(Session session, Vector2? startPosition = default);
        [MonoModConstructor]
        public void ctor(Session session, Vector2? startPosition = default) {
            Logger.Log(LogLevel.Info, "LevelLoader", $"Loading {session?.Area.GetSID() ?? "NULL"}");

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

            // Clear any custom tileset sound paths
            patch_SurfaceIndex.IndexToCustomPath.Clear();

            string path = "";

            try {
                AreaData area = AreaData.Get(session);
                MapMeta meta = area.GetMeta();

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
                XmlNodeList animatedData = Calc.LoadContentXML(path).GetElementsByTagName("sprite");
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
                    SpriteBank bankMod = new SpriteBank(GFX.Game, getModdedSpritesXml(path));

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
            } catch (Exception e) {
                string sid = session?.Area.GetSID() ?? "NULL";
                if (patch_LevelEnter.ErrorMessage == null) {
                    if (e is XmlException) {
                        patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_xmlerror").Replace("((path))", path);
                        Logger.Log(LogLevel.Warn, "LevelLoader", $"Failed parsing {path}");
                    } else if (e.TypeInStacktrace(typeof(Autotiler))) {
                        patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_tilexmlerror").Replace("((path))", path);
                        Logger.Log(LogLevel.Warn, "LevelLoader", $"Failed parsing tileset tag in {path}");
                    } else {
                        patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_levelloadfailed").Replace("((sid))", sid);
                    }
                }
                Logger.Log(LogLevel.Warn, "LevelLoader", $"Failed loading {sid}");
                e.LogDetailed();
            }

            orig_ctor(session, startPosition);

            if (patch_LevelEnter.ErrorMessage == null) {
                RunThread.Start(new Action(LoadingThread_Safe), "LEVEL_LOADER");
                LastLoadingThread = patch_RunThread.Current;

                // get rid of all entities in the pooler to make sure they don't keep references to the previous level.
                foreach (Queue<Entity> entities in ((patch_Pooler) Engine.Pooler).Pools.Values) {
                    entities.Clear();
                }
            } else {
                Loaded = true; // We encountered an error, so skip the loading screen
            }
        }

        private XmlDocument getModdedSpritesXml(string path) {
            XmlDocument vanillaSpritesXml = patch_Calc.orig_LoadContentXML(Path.Combine("Graphics", "Sprites.xml"));
            XmlDocument modSpritesXml = Calc.LoadContentXML(path);
            return patch_SpriteBank.GetSpriteBankExcludingVanillaCopyPastes(vanillaSpritesXml, modSpritesXml, path);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchLoadingThreadAddEvent] // ... except for manually manipulating the method via MonoModRules
        [PatchLoadingThreadAddSubHudRenderer] 
        private extern void LoadingThread();

        private void LoadingThread_Safe() {
            try {
                LoadingThread();
            } catch (Exception e) {
                string sid = session?.Area.GetSID() ?? "NULL";
                if (patch_LevelEnter.ErrorMessage == null) {
                    if (e is AutotilerException ex && e.Source == "TileHandler") {
                        string room = "???";
                        for (int i = 0; i < GFX.FGAutotiler.LevelBounds.Count; i++) {
                            if (GFX.FGAutotiler.LevelBounds[i].Contains(ex.X, ex.Y)) {
                                ex.X -= GFX.FGAutotiler.LevelBounds[i].X;
                                ex.Y -= GFX.FGAutotiler.LevelBounds[i].Y;
                                room = session.MapData.Levels[i].Name;
                                break;
                            }
                        }
                        
                        string type = "";
                        if (e.TypeInStacktrace(typeof(SolidTiles))) {
                            type = "fg";
                        } else if (e.TypeInStacktrace(typeof(BackgroundTiles))) {
                            type = "bg";
                        }
                        
                        patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_badtileid")
                            .Replace("((type))", type).Replace("((id))", ex.ID.ToString()).Replace("((x))", ex.X.ToString())
                            .Replace("((y))", ex.Y.ToString()).Replace("((room))", room).Replace("((sid))", sid);
                        Logger.Log(LogLevel.Warn, "LevelLoader", $"Undefined tile id '{ex.ID}' at ({ex.X}, {ex.Y}) in room {room}");
                    } else {
                        patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_levelloadfailed").Replace("((sid))", sid);
                    }
                }
                Logger.Log(LogLevel.Warn, "LevelLoader", $"Failed loading {sid}");
                e.LogDetailed();
                Loaded = true;
            }
        }

        [MonoModIgnore]
        [MonoModLinkTo("Monocle.Scene", "System.Void Update()")]
        public extern void base_Update();

        [MonoModIgnore]
        private extern void StartLevel();

        [MonoModReplace]
        public override void Update() {
            base_Update();
            if (Loaded && !started) {
                if (patch_LevelEnter.ErrorMessage == null) {
                    StartLevel();
                }
                else {
                    LevelEnter.Go(session, false); // We encountered an error, so display the error screen
                }
            }
        }

    }
}

namespace MonoMod {

    /// <summary>
    /// Removes the level loading thread call so we can create it in the new constructor instead.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLevelLoaderOrigCtor))]
    class PatchLevelLoaderOrigCtorAttribute : Attribute { }

    /// <summary>
    /// Adds a SubHudRenderer to the level in the loader thread.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLoadingThreadAddSubHudRenderer))]
    class PatchLoadingThreadAddSubHudRendererAttribute : Attribute { }

    /// <summary>
    /// Invokes the OnLoadingThread event at the end of the loading thread.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLoadingThreadAddEvent))]
    class PatchLoadingThreadAddEventAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchLevelLoaderOrigCtor(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt("Celeste.LevelLoader", "set_Level"));
            // removes RunThread.Start(new Action(this.LoadingThread), "LEVEL_LOADER", false);
            cursor.RemoveRange(6);
        }

        public static void PatchLoadingThreadAddSubHudRenderer(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_Level = MonoModRule.Modder.FindType("Celeste.Level").Resolve();
            FieldDefinition f_SubHudRenderer = t_Level.FindField("SubHudRenderer");
            MethodDefinition ctor_SubHudRenderer = f_SubHudRenderer.FieldType.Resolve().FindMethod("System.Void .ctor()");

            // Add a local variable we'll use to store a SubHudRenderer object temporarily
            VariableDefinition loc_SubHudRenderer_0 = new VariableDefinition(f_SubHudRenderer.FieldType);
            context.Body.Variables.Add(loc_SubHudRenderer_0);

            /*
            We just want to add
            this.Level.Add(this.Level.SubHudRenderer = new SubHudRenderer());
            before
            this.Level.Add(this.Level.HudRenderer = new HudRenderer());
            */

            ILCursor cursor = new ILCursor(context);
            // Got to the point just before we want to add our code, making use of the Level objects loaded for the HudRenderer
            cursor.GotoNext(instr => instr.MatchNewobj("Celeste.HudRenderer"));
            // Retrieve methods we want to use from around the target instruction
            MethodReference m_LevelLoader_get_Level = (MethodReference) cursor.Prev.Operand;
            cursor.FindNext(out ILCursor[] cursors, instr => instr.MatchCallvirt("Monocle.Scene", "Add"));
            MethodReference m_Scene_Add = (MethodReference) cursors[0].Next.Operand;

            // Load the new renderer onto the stack and duplicate it.
            cursor.Emit(OpCodes.Newobj, ctor_SubHudRenderer);
            cursor.Emit(OpCodes.Dup);
            // Store one copy in a local variable for later
            cursor.Emit(OpCodes.Stloc_S, loc_SubHudRenderer_0);
            // Store the other copy in its field
            cursor.Emit(OpCodes.Stfld, f_SubHudRenderer);
            // Load the first copy back onto the stack
            cursor.Emit(OpCodes.Ldloc_S, loc_SubHudRenderer_0);
            // And add it to the scene
            cursor.Emit(OpCodes.Callvirt, m_Scene_Add);

            // We could have dup'd the pre-existing Level object, but this produces a cleaner decomp (replacing the Level objects we cannibalized)
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_LevelLoader_get_Level);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_LevelLoader_get_Level);
        }

        public static void PatchLoadingThreadAddEvent(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_LevelLoader_get_Level = context.Method.DeclaringType.FindMethod("Celeste.Level get_Level()");
            MethodDefinition m_Everest_Events_LevelLoader_LoadingThread = MonoModRule.Modder.Module.GetType("Celeste.Mod.Everest/Events/LevelLoader").FindMethod("System.Void LoadingThread(Celeste.Level)");

            ILCursor cursor = new ILCursor(context);

            // We want to move to just before the end of the loading thread and invoke an event for mods to hook
            cursor.GotoNext(MoveType.After, instr => instr.MatchStfld("Celeste.Level", "Pathfinder"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_LevelLoader_get_Level);
            cursor.Emit(OpCodes.Call, m_Everest_Events_LevelLoader_LoadingThread);
        }

    }
}
