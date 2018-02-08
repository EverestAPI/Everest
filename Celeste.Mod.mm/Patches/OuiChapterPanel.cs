#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OuiChapterPanel : OuiChapterPanel {

        private bool instantClose = false;

        [MonoModReplace]
        public static new string GetCheckpointPreviewName(AreaKey area, string level) {
            string result = area.ToString();
            if (area.ID >= 10)
                result = area.GetSID();
            if (level != null)
                result = result + "_" + level;
            return result;
        }

        public extern bool orig_IsStart(Overworld overworld, Overworld.StartMode start);
        public override bool IsStart(Overworld overworld, Overworld.StartMode start) {
            if (SaveData.Instance != null && SaveData.Instance.LastArea.ID == AreaKey.None.ID) {
                SaveData.Instance.LastArea = AreaKey.Default;
                instantClose = true;
            }
            return orig_IsStart(overworld, start);
        }

        public extern IEnumerator orig_Enter(Oui from);
        public override IEnumerator Enter(Oui from) {
            if (instantClose)
                return EnterClose(from);
            return orig_Enter(from);
        }

        private IEnumerator EnterClose(Oui from) {
            Overworld.Goto<OuiChapterSelect>();
            Visible = false;
            instantClose = false;
            yield break;
        }

        public extern void orig_Update();
        public override void Update() {
            if (instantClose) {
                Overworld.Goto<OuiChapterSelect>();
                Visible = false;
                instantClose = false;
                return;
            }
            orig_Update();
        }

    }
}
