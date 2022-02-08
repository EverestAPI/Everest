
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
            foreach (UpdateWrappingComponent component in Components.GetAll<UpdateWrappingComponent>()) {
                component.PreUpdate();
            }
        }

        internal void UpdateFinalizer() {
            foreach (UpdateWrappingComponent component in Components.GetAll<UpdateWrappingComponent>()) {
                component.PostUpdate();
            }
        }
    }
}
