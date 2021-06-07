using Monocle;

namespace Celeste.Mod.Entities {
    public class SceneRenderer : Renderer {

        public Scene WrappedScene;

        private bool allocated = false;
        private VirtualRenderTarget HudTarget;

        public SceneRenderer(Scene scene) {
            WrappedScene = scene;
        }

        public void Alloc() {
            if (allocated)
                return;
            allocated = true;

            HudTarget = VirtualContent.CreateRenderTarget("hud-target", 1922, 1082);
        }

        public void Dispose() {
            if (!allocated)
                return;
            allocated = false;

            HudTarget.Dispose();
            HudTarget = null;
        }

        public VirtualRenderTarget SwapHudTarget(VirtualRenderTarget target) {
            VirtualRenderTarget prev = Celeste.HudTarget;
            Celeste.HudTarget = target;
            return prev;
        }

        public override void Update(Scene scene) {
            base.Update(scene);
            bool inputDisabled = MInput.Disabled;
            MInput.Disabled = false;
            WrappedScene.BeforeUpdate();
            WrappedScene.Update();
            WrappedScene.AfterUpdate();
            MInput.Disabled = inputDisabled;
        }

        public override void BeforeRender(Scene scene) {
            base.BeforeRender(scene);
            VirtualRenderTarget prev = SwapHudTarget(HudTarget);
            WrappedScene.BeforeRender();
            SwapHudTarget(prev);
        }

        public override void Render(Scene scene) {
            base.Render(scene);
        }

        public override void AfterRender(Scene scene) {
            base.AfterRender(scene);
            VirtualRenderTarget prev = SwapHudTarget(HudTarget);
            WrappedScene.Render();
            WrappedScene.AfterRender();
            SwapHudTarget(prev);
        }

    }
}
