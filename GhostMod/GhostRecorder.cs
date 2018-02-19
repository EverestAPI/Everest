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
    public class GhostRecorder : Component {

        public GhostData Data;

        public GhostRecorder()
            : base(true, false) {
        }

        public override void Added(Entity entity) {
            base.Added(entity);

            if (!GhostModule.Settings.Enabled) {
                RemoveSelf();
                return;
            }
        }

        public override void Update() {
            base.Update();

            Player player = (Player) Entity;
            Data.Frames.Add(new GhostFrame {
                Position = player.Position,
                Speed = player.Speed,
                Rotation = player.Sprite.Rotation,
                Scale = player.Sprite.Scale,
                Color = player.Sprite.Color,

                Facing = player.Facing,

                CurrentAnimationID = player.Sprite.CurrentAnimationID,
                CurrentAnimationFrame = player.Sprite.CurrentAnimationFrame,

                HairColor = player.Hair.Color,
                HairSimulateMotion = player.Hair.SimulateMotion
            });
        }

    }
}
