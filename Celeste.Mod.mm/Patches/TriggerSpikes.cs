using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    public class patch_TriggerSpikes : TriggerSpikes {

        private const float DelayTime = 0.4f;
        private float customDelayTime;

        public patch_TriggerSpikes(EntityData data, Vector2 offset, Directions dir) : base(data, offset, dir) {
            // dummy constructor
        }

        // do some cabling to expose the old constructor unchanged + a new constructor with the new "delayTime" setting
        // + the (EntityData, Vector2, Directions) constructor wired to the new one.

        [MonoModConstructor]
        [MonoModIgnore]
        public extern void ctor(Vector2 position, int size, Directions direction);

        [MonoModIgnore]
        private static extern int GetSize(EntityData data, Directions dir);

        [MonoModConstructor]
        [MonoModReplace]
        public void ctor(EntityData data, Vector2 offset, Directions dir) {
            ctor(data.Position + offset, GetSize(data, dir), dir, data.Float("delayTime", DelayTime));
        }

        [MonoModConstructor]
        [MonoModReplace]
        public void ctor(Vector2 position, int size, Directions direction, float delayTime) {
            ctor(position, size, direction);
            customDelayTime = delayTime;
        }

        // inject the custom delay time in the spike info, instead of the hardcoded 0.4 time.

        private struct SpikeInfo {
            [MonoModIgnore]
            [PatchTriggerSpikesDelayTime]
            public extern bool OnPlayer(Player player, Vector2 outwards);
        }
    }
}
