#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_CheckpointData : CheckpointData {

        public AreaKey Area;

        public patch_CheckpointData(string level, string name, PlayerInventory? inventory = null, bool dreaming = false, AudioState audioState = null)
            : base(level, name, inventory, dreaming, audioState) {
        }

    }
    public static class CheckpointDataExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static AreaKey GetArea(this CheckpointData self)
            => ((patch_CheckpointData) self).Area;
        public static void SetArea(this CheckpointData self, AreaKey value)
            => ((patch_CheckpointData) self).Area = value;

    }
}
