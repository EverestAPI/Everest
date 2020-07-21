#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it


namespace Celeste {
    class patch_AudioState : AudioState {

        public void Apply() {
            Apply(false);
        }

    }
}
