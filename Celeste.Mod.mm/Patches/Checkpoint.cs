#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;

namespace Celeste {
    class patch_Checkpoint : Checkpoint {

        private string bg;
        private Image image;
        private Sprite sprite;
        private Sprite flash;

        public patch_Checkpoint(Vector2 position, string bg = "", Vector2? spawnTarget = null)
            : base(position, bg, spawnTarget) {
            //no-op
        }

        [MonoModLinkTo("Monocle.Entity", "Awake")]
        [MonoModIgnore]
        public extern void base_Awake(Scene scene);
        public override void Awake(Scene scene) {
            base_Awake(scene);

            Level level = scene as Level;
            if (level == null || level.Session.Area.GetLevelSet() == "Celeste")
                return;

            if (image != null || !string.IsNullOrWhiteSpace(bg))
                return;

            string id = "objects/checkpoint/bg/" + level.Session.Area.GetSID();
            if (GFX.Game.Has(id)) {
                Add(image = new Image(GFX.Game[id]));
                image.JustifyOrigin(0.5f, 1f);
                return;
            }

            // Almost all mod maps from pre-1.3.0.0 have got their checkpoints
            // misplaced, as the checkpoint entity wasn't visible in-game.
            // Decals were used instead, so let's just move the entity.

            foreach (patch_Decal decal in level.Entities.FindAll<Decal>()) {
                if (decal.Name.IndexOf("checkpoint", StringComparison.InvariantCultureIgnoreCase) == -1)
                    continue;

                Depth = decal.Depth - 1;
                Vector2 scale = sprite.Scale = flash.Scale = decal.Scale;
                Position = decal.Position + (
                    decal.Name == "decals/1-forsakencity/checkpoint" ? new Vector2(0f, 13f) :
                    new Vector2(0f, 12f)
                ) * scale;

                return;
            }

            // Worst case: Let's just hide it.
            // This hides it in old mod maps which don't have any
            // checkpoint indicator. Not hiding this would show
            // the checkpoint indicator in a weird place.
            Visible = false;
        }

        public override void Render() {
            if (!Visible)
                return;
            base.Render();
        }

        private extern void orig_TurnOn(bool animate);
        private void TurnOn(bool animate) {
            if (!Visible)
                return;
            orig_TurnOn(animate);
        }

    }
}
