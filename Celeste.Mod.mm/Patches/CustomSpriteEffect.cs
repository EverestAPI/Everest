using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;

namespace Celeste {
    public class patch_CustomSpriteEffect : CustomSpriteEffect {

        private EffectParameter matrixParam = default;

        [MonoModIgnore]
        public patch_CustomSpriteEffect(Effect effect) : base(effect) {}

        [MonoModLinkTo("Microsoft.Xna.Framework.Graphics.Effect", "OnApply")]
        [MonoModIgnore]
        public extern void base_OnApply();

        [MonoModReplace]
        [MonoModIfFlag("RelinkXNA")]
        protected new void OnApply() {
            Viewport viewport = GraphicsDevice.Viewport;
            Matrix mat = Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, 0f, 1f);
            matrixParam.SetValue(mat); // XNA added a half-pixel translation here
            base_OnApply();
        }

    }
}