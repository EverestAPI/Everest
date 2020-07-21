using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Based on CoreMessage, to be used by custom maps.
    /// 
    /// Checks for the following new attributes:
    /// - `string dialog` (default: `app_ending`)
    /// - `bool outline` (default: `false`)
    /// </summary>
    [CustomEntity("everest/coreMessage")]
    public class CustomCoreMessage : Entity {

        private string text;
        private float alpha;
        private bool outline;

        public CustomCoreMessage(EntityData data, Vector2 offset)
            : base(data.Position + offset) {
            Tag = Tags.HUD;
            text = Dialog.Clean(data.Attr("dialog", "app_ending")).Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[data.Int("line", 0)];
            outline = data.Bool("outline");
        }

        public override void Update() {
            Player entity = Scene.Tracker.GetEntity<Player>();
            if (entity != null) {
                alpha = Ease.CubeInOut(Calc.ClampedMap(Math.Abs(X - entity.X), 0f, 128f, 1f, 0f));
            }
            base.Update();
        }

        public override void Render() {
            Vector2 cam = ((Level) Scene).Camera.Position;
            Vector2 posTmp = cam + new Vector2(160f, 90f);
            Vector2 pos = (Position - cam + (Position - posTmp) * 0.2f) * 6f;
            if (SaveData.Instance != null && SaveData.Instance.Assists.MirrorMode)
                pos.X = 1920f - pos.X;
            if (outline)
                ActiveFont.DrawOutline(text, pos, new Vector2(0.5f, 0.5f), Vector2.One * 1.25f, Color.White * alpha, 2f, Color.Black * alpha);
            else
                ActiveFont.Draw(text, pos, new Vector2(0.5f, 0.5f), Vector2.One * 1.25f, Color.White * alpha);
        }

    }
}
