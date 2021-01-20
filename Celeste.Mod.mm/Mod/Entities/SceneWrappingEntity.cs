using Monocle;
using System;

namespace Celeste.Mod.Entities {
    public class SceneWrappingEntity : Entity {

        public readonly Scene WrappedScene;
        public readonly SceneRenderer Renderer;

        public bool Initialized { get; private set; }

        public event Action<Scene> OnBegin;
        public event Action<Scene> OnEnd;

        public SceneWrappingEntity(Scene scene) {
            WrappedScene = scene;
            Renderer = new SceneRenderer(scene);
        }

        private void Initialize(Scene scene) {
            if (Initialized)
                return;
            Initialized = true;

            WrappedScene.Begin();
            scene.RendererList.Add(Renderer);
            Renderer.Alloc();
            OnBegin?.Invoke(WrappedScene);
        }

        private void Uninitialize(Scene scene) {
            if (!Initialized)
                return;
            Initialized = false;

            WrappedScene.End();
            scene.RendererList.Remove(Renderer);
            Renderer.Dispose();
            OnEnd?.Invoke(WrappedScene);
        }

        public override void SceneBegin(Scene scene) {
            base.SceneBegin(scene);
            Initialize(scene);
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);
            Initialize(scene);
        }

        public override void HandleGraphicsCreate() {
            base.HandleGraphicsCreate();
            WrappedScene.HandleGraphicsCreate();
        }

        public override void HandleGraphicsReset() {
            base.HandleGraphicsReset();
            WrappedScene.HandleGraphicsReset();
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);
            Uninitialize(scene);
        }

        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);
            Uninitialize(scene);
        }

    }

    public class SceneWrappingEntity<T> : SceneWrappingEntity where T : Scene {

        public readonly new T WrappedScene;

        public new event Action<T> OnBegin;
        public new event Action<T> OnEnd;

        public SceneWrappingEntity(T scene) : base(scene) {
            WrappedScene = scene;

            base.OnBegin += _ => OnBegin?.Invoke(WrappedScene);
            base.OnEnd += _ => OnEnd?.Invoke(WrappedScene);
        }

    }
}
