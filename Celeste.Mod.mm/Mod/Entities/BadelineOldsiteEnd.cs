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
    [Tracked]
    /// <summary>
    /// Custom "end zone" for BadelineOldsite in custom levels.
    /// </summary>
    public class BadelineOldsiteEnd : Entity {

        public BadelineOldsiteEnd(Vector2 position, int width, int height)
            : base(position) {
            Collider = new Hitbox(width, height, 0f, 0f);
        }

        public BadelineOldsiteEnd(EntityData data, Vector2 offset)
            : this(data.Position + offset, data.Width, data.Height) {
        }

    }
}
