using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    // Based on Memorial
    public class CustomMemorial : Entity {

        private Image sprite;
        private CustomMemorialText text;
        private Sprite dreamyText;
        private bool wasShowing;
        private SoundSource loopingSfx;

        private string textText;

        public CustomMemorial(Vector2 position, string texture, string dialog)
            : base(position) {
            Tag = Tags.PauseUpdate;
            Add(sprite = new Image(GFX.Game[texture]));
            sprite.Origin = new Vector2(sprite.Width / 2f, sprite.Height);

            textText = Dialog.Clean(dialog);

            Depth = 100;

            Collider = new Hitbox(60f, 80f, -30f, -60f);

            Add(loopingSfx = new SoundSource());
        }

        public CustomMemorial(EntityData data, Vector2 offset)
            : this(data.Position + offset, data.Attr("sprite", "scenery/memorial/memorial"), data.Attr("dialog", "memorial")) {
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            Level level = (Level) scene;

            level.Add(text = new CustomMemorialText(this, level.Session.Dreaming, textText));

            if (level.Session.Dreaming) {
                Add(dreamyText = new Sprite(GFX.Game, "scenery/memorial/floatytext"));
                dreamyText.AddLoop("dreamy", "", 0.1f);
                dreamyText.Play("dreamy", false, false);
                dreamyText.Position = new Vector2(-dreamyText.Width / 2f, -33f);
            }

            if (level.Session.Area.ID == 1 && level.Session.Area.Mode == AreaMode.Normal) {
                Audio.SetMusicParam("end", 1f);
            }
        }

        public override void Update() {
            base.Update();

            Level level = (Level) Scene;

            if (level.Paused) {
                loopingSfx.Pause();
                return;
            }

            Player player = Scene.Tracker.GetEntity<Player>();

            bool dreaming = level.Session.Dreaming;
            wasShowing = text.Show;
            text.Show = player != null && CollideCheck(player);

            if (text.Show && !wasShowing) {
                Audio.Play(dreaming ? "event:/ui/game/memorial_dream_text_in" : "event:/ui/game/memorial_text_in");
                if (dreaming) {
                    loopingSfx.Play("event:/ui/game/memorial_dream_loop", null, 0f);
                    loopingSfx.Param("end", 0f);
                }

            } else if (!text.Show && wasShowing) {
                Audio.Play(dreaming ? "event:/ui/game/memorial_dream_text_out" : "event:/ui/game/memorial_text_out");
                loopingSfx.Param("end", 1f);
                loopingSfx.Stop(true);
            }

            loopingSfx.Resume();
        }

    }
}
