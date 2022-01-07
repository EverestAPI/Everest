
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
