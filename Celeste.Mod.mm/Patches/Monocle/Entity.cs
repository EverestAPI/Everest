
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
            foreach (Component _component in Components) {
                if(_component is UpdateWrappingComponent component)
                    component.PreUpdate?.Invoke(this);
            }
        }

        internal void UpdateFinalizer() {
            foreach (Component _component in Components) {
                if (_component is UpdateWrappingComponent component)
                    component.PostUpdate?.Invoke(this);
            }
        }
    }
}
