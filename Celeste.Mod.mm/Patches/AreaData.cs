#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_AreaData : AreaData {

        public static extern void orig_Load();
        public static new void Load() {
            orig_Load();
            Everest.Events.AreaData.Load();
        }

        public static extern void orig_ReloadMountainViews();
        public static new void ReloadMountainViews() {
            orig_ReloadMountainViews();
            Everest.Events.AreaData.ReloadMountainViews();
        }

    }
}
