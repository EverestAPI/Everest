#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;

namespace Monocle {
    class patch_Tween {
        public Action<Tween> OnUpdate;
        public Action<Tween> OnComplete;
        public Action<Tween> OnStart;

        public extern void orig_Removed(Entity entity);
        public void Removed(Entity entity) {
            orig_Removed(entity);
            OnUpdate = (OnComplete = (OnStart = null));
        }
    }
}
