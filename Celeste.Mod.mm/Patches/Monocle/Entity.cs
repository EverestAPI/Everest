
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
            foreach (PreUpdateComponent puc in Components) {
                    puc.PreUpdate();
            }
        }

        internal void UpdateFinalizer() {
            foreach (PostUpdateComponent puc in Components) {
                    puc.PostUpdate();
            }
        }
    }
}
