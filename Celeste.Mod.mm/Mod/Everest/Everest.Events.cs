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

using _OuiMainMenu = Celeste.OuiMainMenu;
using _Level = Celeste.Level;
using _Player = Celeste.Player;
using _OuiJournal = Celeste.OuiJournal;
using _Decal = Celeste.Decal;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Events {

            public static class Celeste {
                public static event Action OnExiting;
                internal static void Exiting()
                    => OnExiting?.Invoke();

                public static event Action OnShutdown;
                internal static void Shutdown()
                    => OnShutdown?.Invoke();
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

        }
    }
}
