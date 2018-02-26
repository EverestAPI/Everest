using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiHelper_Shutdown : Oui {

        public OuiHelper_Shutdown() {
        }

        public override IEnumerator Enter(Oui from) {
            Focused = false;
            new FadeWipe(Scene, false, delegate {
                Engine.Scene = new Scene();
                Engine.Instance.Exit();
            });
            yield break;
        }

        public override IEnumerator Leave(Oui next) {
            yield break;
        }

    }
}
