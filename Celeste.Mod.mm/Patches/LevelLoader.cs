#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    class patch_LevelLoader : LevelLoader {

        public patch_LevelLoader(Session session, Vector2? startPosition = default(Vector2?))
            : base(session, startPosition) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor_LevelLoader(Session session, Vector2? startPosition = default(Vector2?));
        [MonoModConstructor]
        public void ctor_LevelLoader(Session session, Vector2? startPosition = default(Vector2?)) {
            if (CoreModule.Settings.LazyLoading)
                VirtualContentExt.UnloadOverworld();
            orig_ctor_LevelLoader(session, startPosition);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchLevelLoaderThread] // ... except for manually manipulating the method via MonoModRules
        private extern void LoadingThread();

    }
}
