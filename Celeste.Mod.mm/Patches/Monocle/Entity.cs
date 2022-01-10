
using MonoMod;
using Celeste.Mod.Entities;

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
        internal void UpdatePreceder() {
            foreach (IPreUpdateComponent pc in Components) {
                pc.PreUpdate();
            }
        }

        internal void UpdateFinalizer() {
            foreach (IPostUpdateComponent pc in Components) {
                pc.PostUpdate();
            }
        }
    }
}
