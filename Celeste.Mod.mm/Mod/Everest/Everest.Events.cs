using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using _Atlas = Monocle.Atlas;
using _OuiMainMenu = Celeste.OuiMainMenu;
using _Level = Celeste.Level;
using _Player = Celeste.Player;
using _OuiJournal = Celeste.OuiJournal;

namespace Celeste.Mod {
    public static partial class Everest {
        [Obsolete("Please use HookGen (aka MMHOOK_Celeste.dll) instead.")]
        public static class Events {

            public static class Celeste {
                public static event Action OnExiting;
                internal static void Exiting()
                    => OnExiting?.Invoke();

                public static event Action OnShutdown;
                internal static void Shutdown()
                    => OnShutdown?.Invoke();
            }

            public static class GFX {

                public static event Action OnLoadGame;
                internal static void LoadGame()
                    => OnLoadGame?.Invoke();

                public static event Action OnUnloadGame;
                internal static void UnloadGame()
                    => OnUnloadGame?.Invoke();

                public static event Action OnLoadGui;
                internal static void LoadGui()
                    => OnLoadGui?.Invoke();

                public static event Action OnUnloadGui;
                internal static void UnloadGui()
                    => OnUnloadGui?.Invoke();

                public static event Action OnLoadOverworld;
                internal static void LoadOverworld()
                    => OnLoadOverworld?.Invoke();

                public static event Action OnUnloadOverworld;
                internal static void UnloadOverworld()
                    => OnUnloadOverworld?.Invoke();

                public static event Action OnLoadMountain;
                internal static void LoadMountain()
                    => OnLoadMountain?.Invoke();

                public static event Action OnUnloadMountain;
                internal static void UnloadMountain()
                    => OnUnloadMountain?.Invoke();

                public static event Action OnLoadOther;
                internal static void LoadOther()
                    => OnLoadOther?.Invoke();

                public static event Action OnUnloadOther;
                internal static void UnloadOther()
                    => OnUnloadOther?.Invoke();

                public static event Action OnLoadPortraits;
                internal static void LoadPortraits()
                    => OnLoadPortraits?.Invoke();

                public static event Action OnUnloadPortraits;
                internal static void UnloadPortraits()
                    => OnUnloadPortraits?.Invoke();

                public static event Action OnLoadData;
                internal static void LoadData()
                    => OnLoadData?.Invoke();

                public static event Action OnUnloadData;
                internal static void UnloadData()
                    => OnUnloadData?.Invoke();

                public static event Action OnLoadEffects;
                internal static void LoadEffects()
                    => OnLoadEffects?.Invoke();

            }

            public static class AreaData {

                public static event Action OnLoad;
                internal static void Load()
                    => OnLoad?.Invoke();

                public static event Action OnReloadMountainViews;
                internal static void ReloadMountainViews()
                    => OnReloadMountainViews?.Invoke();

            }

            public static class Atlas {
                public static event Action<_Atlas> OnLoad;
                internal static void Load(_Atlas atlas)
                    => OnLoad?.Invoke(atlas);
            }

            public static class Dialog {
                public static event Action OnInitLanguages;
                internal static void InitLanguages()
                    => OnInitLanguages?.Invoke();
            }

            public static class OuiMainMenu {
                public delegate void CreateButtonsHandler(_OuiMainMenu menu, List<MenuButton> buttons);
                public static event CreateButtonsHandler OnCreateButtons;
                internal static void CreateButtons(_OuiMainMenu menu, List<MenuButton> buttons)
                    => OnCreateButtons?.Invoke(menu, buttons);
            }

            public static class Level {

                public delegate void PauseHandler(_Level level, int startIndex, bool minimal, bool quickReset);
                public static event PauseHandler OnPause;
                internal static void Pause(_Level level, int startIndex, bool minimal, bool quickReset)
                    => OnPause?.Invoke(level, startIndex, minimal, quickReset);

                public delegate void CreatePauseMenuButtonsHandler(_Level level, TextMenu menu, bool minimal);
                public static event CreatePauseMenuButtonsHandler OnCreatePauseMenuButtons;
                internal static void CreatePauseMenuButtons(_Level level, TextMenu menu, bool minimal)
                    => OnCreatePauseMenuButtons?.Invoke(level, menu, minimal);

                public delegate void TransitionToHandler(_Level level, LevelData next, Vector2 direction);
                public static event TransitionToHandler OnTransitionTo;
                internal static void TransitionTo(_Level level, LevelData next, Vector2 direction)
                    => OnTransitionTo?.Invoke(level, next, direction);

                public delegate bool LoadEntityHandler(_Level level, LevelData levelData, Vector2 offset, EntityData entityData);
                public static event LoadEntityHandler OnLoadEntity;
                internal static bool LoadEntity(_Level level, LevelData levelData, Vector2 offset, EntityData entityData)
                    => OnLoadEntity?.InvokeWhileFalse(level, levelData, offset, entityData) ?? false;

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

            }

            public static class Input {

                public static event Action OnInitialize;
                internal static void Initialize()
                    => OnInitialize?.Invoke();

                public static event Action OnDeregister;
                internal static void Deregister()
                    => OnDeregister?.Invoke();

            }

            public static class OuiJournal {
                public delegate void EnterHandler(_OuiJournal journal, Oui from);
                public static event EnterHandler OnEnter;
                internal static void Enter(_OuiJournal journal, Oui from)
                    => OnEnter?.Invoke(journal, from);
            }

        }
    }
}
