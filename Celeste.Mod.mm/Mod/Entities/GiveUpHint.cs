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
    /// Semi-internal entity that shows a hint when giving up.
    /// </summary>
    internal class GiveUpHint : Entity {

        private string text;

        public GiveUpHint() {
            Tag = Tags.HUD;
            text = Dialog.Clean("UI_GIVEUP_HINT");
        }

        public override void Render() {
            const float scale = 0.75f;
            float width = ActiveFont.Measure(text).X;
            width *= scale;
            ActiveFont.DrawOutline(
                text,
                new Vector2((1920f - width) / 2f, 980f),
                new Vector2(0f, 0.5f),
                Vector2.One * scale,
                Color.LightGray,
                2f, Color.Black
            );
        }

    }
}
