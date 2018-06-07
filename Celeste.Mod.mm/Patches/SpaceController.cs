#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;

namespace Celeste {
    class patch_SpaceController : Entity {

        public override void Awake(Scene scene) {
            base.Awake(scene);

            // Small hack to force CameraTargetTrigger in custom space levels.
            Level level = scene as Level;
            if (level == null ||
                level.Session.Area.GetLevelSet() == "Celeste" ||
                level.Tracker.GetEntity<CameraTargetTrigger>() != null)
                return;

            level.Add(new CameraTargetTrigger(
                new EntityData {
                    Position = Vector2.Zero,
                    Width = level.Bounds.Width,
                    Height = level.Bounds.Height,
                    Values = new Dictionary<string, object>() {
                        { "yOnly", "true" },
                        { "lerpStrength", "1" },
                    },
                    Nodes = new Vector2[] {
                        new Vector2(160f, level.Bounds.Height / 2f)
                    }
                },
                level.LevelOffset
            ));
        }

    }
}
