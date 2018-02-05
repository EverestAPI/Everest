#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_AreaStats : AreaStats {

        public patch_AreaStats(int id)
            : base(id) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // The original method doesn't check if the area actually has the mode.
        // It also iterates by values of AreaMode.
        [MonoModReplace]
        public new void CleanCheckpoints() {
            AreaData area = AreaData.Get(ID);

            for (int i = 0; i < Modes.Length; i++) {
                AreaMode areaMode = (AreaMode) i;
                AreaModeStats areaModeStats = Modes[i];
                ModeProperties modeProperties = null;
                if (area.HasMode(areaMode))
                    modeProperties = area.Mode[i];

                HashSet<string> checkpoints = new HashSet<string>(areaModeStats.Checkpoints);
                areaModeStats.Checkpoints.Clear();

                if (modeProperties != null && modeProperties.Checkpoints != null)
                    foreach (CheckpointData checkpointData in modeProperties.Checkpoints)
                        if (checkpoints.Contains(checkpointData.Level))
                            areaModeStats.Checkpoints.Add(checkpointData.Level);
            }
        }

    }
}
