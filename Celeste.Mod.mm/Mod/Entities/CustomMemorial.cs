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
    /// Based on Memorial, to be used by custom maps.
    /// 
    /// Checks for the following new attributes:
    /// - `string dialog` (default: `memorial`)
    /// - `string sprite` (default: `scenery/memorial/memorial`)
    /// - `float spacing` (default: 8)
    /// - `DreamingMode dreamingMode` (default: BasedOnMetadata)
    /// </summary>
    [CustomEntity("everest/memorial")]
    public class CustomMemorial : Entity {
        public enum DreamingMode {
            On, Off, BasedOnMetadata
        }

        private Image sprite;
        private CustomMemorialText text;
        private Sprite dreamyText;
        private bool wasShowing;
        private SoundSource loopingSfx;

        private string textText;
        private float textSpacing;
        private DreamingMode dreamingMode;

        public CustomMemorial(Vector2 position, string texture, string dialog, float spacing)
            : this(position, texture, dialog, spacing, DreamingMode.BasedOnMetadata) { }

        public CustomMemorial(Vector2 position, string texture, string dialog, float spacing, DreamingMode dreamingMode)
            : base(position) {
            Tag = Tags.PauseUpdate;
            if (!string.IsNullOrWhiteSpace(texture)) {
                Add(sprite = new Image(GFX.Game[texture]));
                sprite.Origin = new Vector2(sprite.Width / 2f, sprite.Height);
            }

            textText = Dialog.Clean(dialog);
            textSpacing = spacing;
            this.dreamingMode = dreamingMode;

            Depth = 100;

            Collider = new Hitbox(60f, 80f, -30f, -60f);

            Add(loopingSfx = new SoundSource());
        }

        public CustomMemorial(EntityData data, Vector2 offset) : this(data.Position + offset, data.Attr("sprite", "scenery/memorial/memorial"),
            data.Attr("dialog", "memorial"), data.Float("spacing", 8f), data.Enum("dreamingMode", DreamingMode.BasedOnMetadata)) { }

        public override void Added(Scene scene) {
            base.Added(scene);

            Level level = (Level) scene;

            level.Add(text = new CustomMemorialText(this, isDreaming(), textText, textSpacing));

            if (isDreaming()) {
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

            bool dreaming = isDreaming();
            wasShowing = text.Show;
            text.Show = player != null && CollideCheck(player);

            if (text.Show && !wasShowing) {
                Audio.Play(dreaming ? SFX.ui_game_memorialdream_text_in : SFX.ui_game_memorial_text_in);
                if (dreaming) {
                    loopingSfx.Play(SFX.ui_game_memorialdream_loop, null, 0f);
                    loopingSfx.Param("end", 0f);
                }

            } else if (!text.Show && wasShowing) {
                Audio.Play(dreaming ? SFX.ui_game_memorialdream_text_out : SFX.ui_game_memorial_text_out);
                loopingSfx.Param("end", 1f);
                loopingSfx.Stop(true);
            }

            loopingSfx.Resume();
        }

        private bool isDreaming() {
            switch (dreamingMode) {
                case DreamingMode.On:
                    return true;
                case DreamingMode.Off:
                    return false;
                default:
                    return (Scene as Level).Session.Dreaming;
            }
        }
    }
}
