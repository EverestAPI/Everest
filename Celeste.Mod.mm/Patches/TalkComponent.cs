using Microsoft.Xna.Framework;
using Monocle;
using System;
using MonoMod;

namespace Celeste {
    class patch_TalkComponent : TalkComponent {

        [MonoModConstructor]
        [MonoModIgnore]
        public patch_TalkComponent(Rectangle bounds, Vector2 drawAt, Action<Player> onTalk, HoverDisplay hoverDisplay = null)
            : base(bounds, drawAt, onTalk, hoverDisplay) {
            // no-op. MonoMod ignores this - we only need this to make the compiler happy.
        }

        public class patch_TalkComponentUI : TalkComponentUI {

            private float alpha;
            private float slide;
            private float timer;

            [MonoModConstructor]
            [MonoModIgnore]
            public patch_TalkComponentUI(TalkComponent handler) : base(handler) {
                // no-op. MonoMod ignores this - we only need this to make the compiler happy.
            }

            [MonoModLinkTo("Monocle.Entity", "Awake")]
            [MonoModIgnore]
            public extern void base_Awake(Scene scene);
            [MonoModReplace]
            public override void Awake(Scene scene) {
                base_Awake(scene);
                if (Handler.Entity?.Collider != null) {
                    // use Collider if available instead of Position, as Position behaves wrongly
                    // (see https://github.com/EverestAPI/Everest/issues/759)
                    if (Scene.CollideCheck<FakeWall>(Handler.Entity.Collider.Bounds)) {
                        alpha = 0f;
                    }
                } else {
                    // Entity has no Collider, fallback to Position
                    if (Handler.Entity == null || Scene.CollideCheck<FakeWall>(Handler.Entity.Position)) {
                        alpha = 0f;
                    }
                }
                // force alpha if the player can interact
                if (Highlighted) {
                    alpha = 1f;
                }
            }

            [MonoModLinkTo("Monocle.Entity", "Update")]
            [MonoModIgnore]
            public extern void base_Update();
            [MonoModReplace]
            public override void Update() {
                timer += Engine.DeltaTime;
                slide = Calc.Approach(slide, Display ? 1 : 0, Engine.DeltaTime * 4f);
                if (Handler.Entity?.Collider != null) {
                    // use Collider if available instead of Position, as Position behaves wrongly
                    // (see https://github.com/EverestAPI/Everest/issues/759)
                    if (alpha < 1f && !Scene.CollideCheck<FakeWall>(Handler.Entity.Collider.Bounds)) {
                        alpha = Calc.Approach(alpha, 1f, 2f * Engine.DeltaTime);
                    }
                } else {
                    // Entity has no Collider, fallback to Position
                    if (alpha < 1f && Handler.Entity != null && !Scene.CollideCheck<FakeWall>(Handler.Entity.Position)) {
                        alpha = 0f;
                    }
                }
                // force alpha if the player can interact
                if (Highlighted) {
                    alpha = 1f;
                }
                base_Update();
            }
        }
    }
}
