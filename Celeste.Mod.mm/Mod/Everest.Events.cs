using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
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

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Events {

            // TODO: Put any events we want to expose (OnLevelLoad, ...) here.

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

        }
    }
}
