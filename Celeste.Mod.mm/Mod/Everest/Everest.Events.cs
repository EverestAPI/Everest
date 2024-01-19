using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using _Decal = Celeste.Decal;
using _EventTrigger = Celeste.EventTrigger;
using _Level = Celeste.Level;
using _OuiJournal = Celeste.OuiJournal;
using _OuiMainMenu = Celeste.OuiMainMenu;
using _Player = Celeste.Player;
using _Seeker = Celeste.Seeker;
using _AngryOshiro = Celeste.AngryOshiro;
using _SubHudRenderer = Celeste.Mod.UI.SubHudRenderer;
using Monocle;

namespace Celeste.Mod {
    public static partial class Everest {
        /// <summary>
        /// Events that are called at various points in the game.
        /// </summary>
        public static class Events {

            public static event Action<CriticalErrorHandler> OnCriticalError;
            internal static void CriticalError(CriticalErrorHandler handler) {
                if (OnCriticalError == null)
                    return;

                foreach (Action<CriticalErrorHandler> deleg in OnCriticalError.GetInvocationList()) {
                    try {
                        deleg(handler);
                    } catch (Exception ex) {
                        Logger.Error("crit-error-handler", $"Error invoking critical error event handler {deleg.Method}:");
                        Logger.LogDetailed(ex, "crit-error-handler");
                    }
                }
            }

            public static class Celeste {
                /// <summary>
                /// Called after the main gameloop has finished running.
                /// </summary>
                public static event Action OnExiting;
                internal static void Exiting()
                    => OnExiting?.Invoke();

                /// <summary>
                /// Called just before the Main method exits.
                /// </summary>
                public static event Action OnShutdown;
                internal static void Shutdown()
                    => OnShutdown?.Invoke();
            }

            public static class Everest {
                public delegate void ModLoadedHandler(EverestModuleMetadata meta);
                /// <summary>
                /// Called when a mod finishes loading.
                /// </summary>
                public static event ModLoadedHandler OnLoadMod;
                internal static void LoadMod(EverestModuleMetadata meta)
                    => OnLoadMod?.Invoke(meta);

                public delegate void RegisterModuleHandler(EverestModule module);
                /// <summary>
                /// Called when a mod is registered.
                /// </summary>
                public static event RegisterModuleHandler OnRegisterModule;
                internal static void RegisterModule(EverestModule module)
                    => OnRegisterModule?.Invoke(module);
            }

            [Obsolete("Use MainMenu instead.")]
            public static class OuiMainMenu {
                public delegate void CreateButtonsHandler(_OuiMainMenu menu, List<MenuButton> buttons);
                public static event CreateButtonsHandler OnCreateButtons {
                    add {
                        MainMenu.OnCreateButtons += (MainMenu.CreateButtonsHandler) value.CastDelegate(typeof(MainMenu.CreateButtonsHandler));
                    }
                    remove {
                        MainMenu.OnCreateButtons -= (MainMenu.CreateButtonsHandler) value.CastDelegate(typeof(MainMenu.CreateButtonsHandler));
                    }
                }
            }

            public static class MainMenu {
                public delegate void CreateButtonsHandler(_OuiMainMenu menu, List<MenuButton> buttons);
                /// <summary>
                /// Called after <see cref="_OuiMainMenu.CreateButtons"/>.
                /// </summary>
                public static event CreateButtonsHandler OnCreateButtons;
                internal static void CreateButtons(_OuiMainMenu menu, List<MenuButton> buttons)
                    => OnCreateButtons?.Invoke(menu, buttons);
            }

            public static class LevelLoader {
                public delegate void LoadingThreadHandler(_Level level);
                /// <summary>
                /// Called at the end of the map loading thread.
                /// </summary>
                public static event LoadingThreadHandler OnLoadingThread;
                internal static void LoadingThread(_Level level)
                    => OnLoadingThread?.Invoke(level);
            }

            public static class Level {
                public delegate void PauseHandler(_Level level, int startIndex, bool minimal, bool quickReset);
                /// <summary>
                /// Called after <see cref="_Level.Pause(int, bool, bool)"/>.
                /// </summary>
                public static event PauseHandler OnPause;
                internal static void Pause(_Level level, int startIndex, bool minimal, bool quickReset)
                    => OnPause?.Invoke(level, startIndex, minimal, quickReset);

                public delegate void UnpauseHandler(_Level level);
                /// <summary>
                /// Called after unpausing the Level.
                /// </summary>
                public static event UnpauseHandler OnUnpause;
                internal static void Unpause(_Level level) => OnUnpause?.Invoke(level);

                public delegate void CreatePauseMenuButtonsHandler(_Level level, patch_TextMenu menu, bool minimal);
                /// <summary>
                /// Called when the Level's pause menu is created.
                /// </summary>
                public static event CreatePauseMenuButtonsHandler OnCreatePauseMenuButtons;
                internal static void CreatePauseMenuButtons(_Level level, patch_TextMenu menu, bool minimal)
                    => OnCreatePauseMenuButtons?.Invoke(level, menu, minimal);

                public delegate void TransitionToHandler(_Level level, LevelData next, Vector2 direction);
                /// <summary>
                /// Called after <see cref="_Level.TransitionTo(LevelData, Vector2)"/>
                /// </summary>
                public static event TransitionToHandler OnTransitionTo;
                internal static void TransitionTo(_Level level, LevelData next, Vector2 direction)
                    => OnTransitionTo?.Invoke(level, next, direction);

                public delegate bool LoadEntityHandler(_Level level, LevelData levelData, Vector2 offset, EntityData entityData);
                /// <summary>
                /// Called during <see cref="patch_Level.LoadCustomEntity(EntityData, _Level)"/>.
                /// </summary>
                public static event LoadEntityHandler OnLoadEntity;
                internal static bool LoadEntity(_Level level, LevelData levelData, Vector2 offset, EntityData entityData) {
                    LoadEntityHandler onLoadEntity = OnLoadEntity;

                    if (onLoadEntity == null)
                        return false;

                    // replicates the InvokeWhileFalse extension method, but hardcoding the type to avoid dynamic dispatch
                    foreach (LoadEntityHandler handler in onLoadEntity.GetInvocationList()) {
                        if (handler(level, levelData, offset, entityData))
                            return true;
                    }

                    return false;
                }

                public delegate Backdrop LoadBackdropHandler(MapData map, BinaryPacker.Element child, BinaryPacker.Element above);
                public static event LoadBackdropHandler OnLoadBackdrop;
                internal static Backdrop LoadBackdrop(MapData map, BinaryPacker.Element child, BinaryPacker.Element above)
                    => OnLoadBackdrop?.InvokeWhileNull<Backdrop>(map, child, above);

                public delegate void LoadLevelHandler(_Level level, _Player.IntroTypes playerIntro, bool isFromLoader);
                public static event LoadLevelHandler OnLoadLevel;
                internal static void LoadLevel(_Level level, _Player.IntroTypes playerIntro, bool isFromLoader)
                    => OnLoadLevel?.Invoke(level, playerIntro, isFromLoader);

                public delegate void EnterHandler(Session session, bool fromSaveData);
                public static event EnterHandler OnEnter;
                internal static void Enter(Session session, bool fromSaveData)
                    => OnEnter?.Invoke(session, fromSaveData);

                public delegate void ExitHandler(_Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow);
                public static event ExitHandler OnExit;
                internal static void Exit(_Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow)
                    => OnExit?.Invoke(level, exit, mode, session, snow);

                public delegate void CompleteHandler(_Level level);
                public static event CompleteHandler OnComplete;
                internal static void Complete(_Level level)
                    => OnComplete?.Invoke(level);
            }

            public static class Player {
                public static event Action<_Player> OnSpawn;
                internal static void Spawn(_Player player)
                    => OnSpawn?.Invoke(player);

                public static event Action<_Player> OnDie;
                internal static void Die(_Player player)
                    => OnDie?.Invoke(player);

                public static event Action<_Player> OnRegisterStates;
                internal static void RegisterStates(_Player player)
                    => OnRegisterStates?.Invoke(player);
            }

            public static class Seeker {
                public static event Action<_Seeker> OnRegisterStates;
                internal static void RegisterStates(_Seeker seeker)
                    => OnRegisterStates?.Invoke(seeker);
            }

            public static class AngryOshiro {
                public static event Action<_AngryOshiro> OnRegisterStates;
                internal static void RegisterStates(_AngryOshiro oshiro)
                    => OnRegisterStates?.Invoke(oshiro);
            }

            public static class Input {
                public static event Action OnInitialize;
                internal static void Initialize()
                    => OnInitialize?.Invoke();

                public static event Action OnDeregister;
                internal static void Deregister()
                    => OnDeregister?.Invoke();
            }

            [Obsolete("Use Journal instead.")]
            public static class OuiJournal {
                public delegate void EnterHandler(_OuiJournal journal, Oui from);
                public static event EnterHandler OnCreateButtons {
                    add {
                        Journal.OnEnter += (Journal.EnterHandler) value.CastDelegate(typeof(Journal.EnterHandler));
                    }
                    remove {
                        Journal.OnEnter -= (Journal.EnterHandler) value.CastDelegate(typeof(Journal.EnterHandler));
                    }
                }
            }

            public static class Journal {
                public delegate void EnterHandler(_OuiJournal journal, Oui from);
                public static event EnterHandler OnEnter;
                internal static void Enter(_OuiJournal journal, Oui from)
                    => OnEnter?.Invoke(journal, from);
            }

            public static class Decal {
                public delegate void DecalRegistryHandler(_Decal decal, DecalRegistry.DecalInfo decalInfo);
                public static event DecalRegistryHandler OnHandleDecalRegistry;
                internal static void HandleDecalRegistry(_Decal decal, DecalRegistry.DecalInfo decalInfo)
                    => OnHandleDecalRegistry?.Invoke(decal, decalInfo);
            }

            public static class FileSelectSlot {
                public delegate void CreateButtonsHandler(List<patch_OuiFileSelectSlot.Button> buttons, OuiFileSelectSlot slot, EverestModuleSaveData modSaveData, bool fileExists);
                public static event CreateButtonsHandler OnCreateButtons;
                internal static void HandleCreateButtons(List<patch_OuiFileSelectSlot.Button> buttons, OuiFileSelectSlot slot, bool fileExists) {
                    if (OnCreateButtons == null) {
                        return;
                    }

                    foreach (Delegate del in OnCreateButtons.GetInvocationList()) {
                        // find the Everest module this delegate belongs to, and load the mod save data from it for the current slot.
                        EverestModule matchingModule = _Modules.Find(module => module.GetType().Assembly == del.Method.DeclaringType.Assembly);
                        EverestModuleSaveData modSaveData = null;
                        if (matchingModule != null) {
                            modSaveData = matchingModule._SaveData;
                        }

                        // call the delegate.
                        del.DynamicInvoke(new object[] { buttons, slot, modSaveData, fileExists });
                    }
                }
            }

            public static class EventTrigger {
                public delegate bool TriggerEventHandler(_EventTrigger trigger, _Player player, string eventID);
                public static event TriggerEventHandler OnEventTrigger;
                internal static bool TriggerEvent(_EventTrigger trigger, _Player player, string eventID)
                    => OnEventTrigger?.InvokeWhileFalse(trigger, player, eventID) ?? false;
            }

            public static class CustomBirdTutorial {
                public delegate object ParseCommandHandler(string command);
                public static event ParseCommandHandler OnParseCommand;
                internal static object ParseCommand(string command)
                    => OnParseCommand?.InvokeWhileNull<object>(command);
            }

            public static class AssetReload {
                public delegate void ReloadHandler(bool silent);
                public static event ReloadHandler OnBeforeReload, OnAfterReload;
                internal static void BeforeReload(bool silent)
                    => OnBeforeReload?.Invoke(silent);
                internal static void AfterReload(bool silent)
                    => OnAfterReload?.Invoke(silent);

                public delegate void ReloadLevelHandler(global::Celeste.Level level);
                public static ReloadLevelHandler OnReloadLevel;
                internal static void ReloadLevel(global::Celeste.Level level)
                    => OnReloadLevel?.Invoke(level);

                public static Action OnReloadAllMaps;
                internal static void ReloadAllMaps()
                    => OnReloadAllMaps?.Invoke();
            }

            public static class SubHudRenderer {
                public delegate void BeforeRenderHandler(_SubHudRenderer renderer, Scene scene);
                public static event BeforeRenderHandler OnBeforeRender;
                internal static void BeforeRender(_SubHudRenderer renderer, Scene scene)
                    => OnBeforeRender?.Invoke(renderer, scene);
            }
        }
    }
}
