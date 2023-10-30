using MonoMod;
using System;

namespace Monocle {
    class patch_Scene : Scene {

        [MonoModIgnore]
    	public new event Action OnEndOfFrame;

        [MonoModReplace]
        public new bool OnInterval(float interval) {
            return (int) (((double) TimeActive - Engine.DeltaTime) / interval) < (int) ((double) TimeActive / interval);
        }

        [MonoModReplace]
        public new bool OnInterval(float interval, float offset) {
            return Math.Floor(((double) TimeActive - offset - Engine.DeltaTime) / interval) < Math.Floor(((double) TimeActive - offset) / interval);
        }

        internal void ClearOnEndOfFrame() => OnEndOfFrame = null;

    }
}
