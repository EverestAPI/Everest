using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Helpers;
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
        public static class Events {

            public static class Celeste {
                public static event Action OnExiting;
                internal static void Exiting()
                    => OnExiting?.Invoke();
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
                public static event Action<_OuiMainMenu, List<MenuButton>> OnCreateButtons;
                internal static void CreateButtons(_OuiMainMenu menu, List<MenuButton> buttons)
                    => OnCreateButtons?.Invoke(menu, buttons);
            }

            public static class Level {

                public static event Action<_Level, int, bool, bool> OnPause;
                internal static void Pause(_Level level, int startIndex, bool minimal, bool quickReset)
                    => OnPause?.Invoke(level, startIndex, minimal, quickReset);

                public static event Action<_Level, TextMenu, bool> OnCreatePauseMenuButtons;
                internal static void CreatePauseMenuButtons(_Level level, TextMenu menu, bool minimal)
                    => OnCreatePauseMenuButtons?.Invoke(level, menu, minimal);

                public static event Action<LevelData, Vector2> OnTransitionTo; 
                internal static void TransitionTo(LevelData next, Vector2 direction)
                    => OnTransitionTo?.Invoke(next, direction);

            }

            public static class LevelEnter {
                public static event Action<Session, bool> OnGo;
                internal static void Go(Session session, bool fromSaveData)
                    => OnGo?.Invoke(session, fromSaveData);
            }

            public static class Player {
                public static event Action<_Player> OnDie;
                internal static void Die(_Player player)
                    => OnDie?.Invoke(player);
            }

            public static class OuiJournal {
                public static event Action<_OuiJournal, Oui> OnEnter;
                internal static void Enter(_OuiJournal journal, Oui from)
                    => OnEnter?.Invoke(journal, from);
            }

            // Put any events we want to expose (f.e. Level.OnLoad) here.

        }
    }
}
