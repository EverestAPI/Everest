using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Based on CoreMessage, to be used by custom maps.
    /// 
    /// Checks for the following new attributes:
    /// - `string dialog` (default: `app_ending`)
    /// </summary>
    public class CustomCoreMessage : Entity {

        private string text;
        private float alpha;

        private bool triggered = false;

        public CustomCoreMessage(EntityData data, Vector2 offset)
            : base(data.Position + offset) {
            Tag = Tags.HUD;
            text = Dialog.Clean(data.Attr("dialog", "app_ending")).Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[data.Int("line", 0)];
        }

        public override void Update() {
            Player entity = Scene.Tracker.GetEntity<Player>();
            if (entity != null) {
                alpha = Ease.CubeInOut(Calc.ClampedMap(Math.Abs(X - entity.X), 0f, 128f, 1f, 0f));
            }
            base.Update();
        }

        public override void Render() {
            Vector2 position = ((Level) Scene).Camera.Position;
            Vector2 value = position + new Vector2(160f, 90f);
            Vector2 position2 = (Position - position + (Position - value) * 0.2f) * 6f;
            ActiveFont.Draw(text, position2, new Vector2(0.5f, 0.5f), Vector2.One * 1.25f, Color.White * alpha);
        }

    }
}
