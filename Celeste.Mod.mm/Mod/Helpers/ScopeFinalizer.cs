using System;

namespace Celeste.Mod.Helpers {
    public sealed class ScopeFinalizer : IDisposable {
        private Action onExit;
        
        public ScopeFinalizer(Action onExit) {
            this.onExit = onExit;
        }

        public void Dispose() {
            onExit();
        }

    }
}