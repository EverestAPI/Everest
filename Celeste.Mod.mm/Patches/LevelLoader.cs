#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod;
using MonoMod;
using System.Collections;
using Monocle;

namespace Celeste {
    class patch_LevelLoader : Scene {

        private extern void orig_StartLevel();
        private void StartLevel() {
            orig_StartLevel();
            Everest.Events.LevelLoader.StartLevel(((LevelLoader) (object) this).Level);
        }

    }
}
