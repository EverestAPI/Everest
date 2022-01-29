
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
            foreach (var component in Components) {
                if(component is IPreUpdateComponent pc)
                    pc.PreUpdate();
            }
        }

        internal void UpdateFinalizer() {
            foreach (var component in Components) {
                if (component is IPostUpdateComponent pc)
                    pc.PostUpdate();
            }
        }
    }
}
