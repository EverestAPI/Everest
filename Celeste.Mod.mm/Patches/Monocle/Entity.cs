
using MonoMod;
using System;
using System.Collections.Generic;

namespace Monocle {
    class patch_Entity : Entity {
        public new Scene Scene {
            [MonoModIgnore]
            get;
            [MonoModIgnore]
            private set;
        }

        internal void DissociateFromScene() {
            Scene = null;
        }

        public List<Action> UpdatePrecederActions;
        public List<Action> UpdateFinalizerActions;

        //For some reason this compiles to be significantly slower than it could be in theory, most likely to do patch structure in MonoMod.
        //If you want me to update this to be written in pure IL I can do that as well, just let me know in PR review
        internal void UpdatePreceder() {
            if(UpdatePrecederActions?.Count > 0) {
                foreach(Action action in UpdateFinalizerActions) {
                    action?.Invoke();
                }
            }
        }

        internal void UpdateFinalizer() {
            if (UpdateFinalizerActions?.Count > 0) { 
                foreach (Action action in UpdateFinalizerActions) {
                    action?.Invoke();
                }
            }
        }
    }
}
