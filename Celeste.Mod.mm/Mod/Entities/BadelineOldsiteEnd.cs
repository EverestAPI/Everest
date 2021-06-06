using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Custom "end zone" for BadelineOldsite in custom levels.
    /// </summary>
    [Tracked]
    [CustomEntity("darkChaserEnd")]
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
