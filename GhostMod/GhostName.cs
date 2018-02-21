using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost {
    public class GhostName : Entity {

        public Entity Tracking;
        public string Name;

        protected Camera Camera;

        public float Alpha;

        public GhostName(Entity tracking, string name)
            : base(Vector2.Zero) {
            Tracking = tracking;
            Name = name;

            Tag = Tags.HUD;
        }

        public override void Render() {
            base.Render();

            if (!GhostModule.Settings.Enabled || !GhostModule.Settings.ShowOtherNames ||
                Tracking == null)
                return;

            if (string.IsNullOrEmpty(Name))
                return;

            Level level = SceneAs<Level>();
            if (level == null || level.FrozenOrPaused || level.RetryPlayerCorpse != null || level.SkippingCutscene)
                return;

            if (Camera == null)
                Camera = level.Camera;
            if (Camera == null)
                return;

            Vector2 pos = Tracking.Position;
            pos.Y -= 16f;
            pos = Camera.CameraToScreen(pos) / Camera.Viewport.Width * 1920f;

            ActiveFont.DrawOutline(
                Name,
                pos,
                new Vector2(0.5f, 1f),
                Vector2.One * 0.5f,
                Color.White * Alpha,
                2f,
                Color.Black * (Alpha * Alpha * Alpha)
            );
        }

    }
}
