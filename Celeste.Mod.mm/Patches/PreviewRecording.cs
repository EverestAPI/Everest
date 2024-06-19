#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_PreviewRecording : PreviewRecording {
        public Session CurrentSession;

        [MonoModIgnore] // We don't want to change anything about the method...
        [MonoModConstructor]
        public patch_PreviewRecording(string filename)
            : base(filename) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [ProxyFileCalls] // ... except for proxying all System.IO.File.* calls to Celeste.Mod.FileProxy.*
        public extern void orig_ctor(string filename);
        
        [MonoModConstructor]
        public void ctor(string filename) {
            orig_ctor(filename);

            if (Engine.Scene is patch_PreviewRecording previewDialog) {
                CurrentSession = previewDialog.CurrentSession;
            } else {
                CurrentSession = (Engine.Scene as Level)?.Session;
            }
        }
        
        public extern void orig_Update();

        public override void Update() {
            orig_Update();
            PressCancelBackToPreviousScene();
        }

        private void PressCancelBackToPreviousScene() {
            if (Input.ESC.Pressed || Input.MenuCancel.Pressed) {
                if (CurrentSession != null) {
                    Engine.Scene = new LevelLoader(CurrentSession);
                } else {
                    Engine.Scene = new OverworldLoader(Overworld.StartMode.Titlescreen);
                }
            }
        }
        
        public extern void orig_Render();
        public override void Render() {
            orig_Render();
            RenderManual();
        }

        private void RenderManual() {
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, RasterizerState.CullNone, null, Engine.ScreenMatrix);
            ActiveFont.Draw("Cancel: Back to Previous Scene", new Vector2(8f, 80f),
                new Vector2(0f, 0f), Vector2.One * 0.5f, Color.White);
            Draw.SpriteBatch.End();
        }
    }
}
