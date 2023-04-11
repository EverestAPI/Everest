using Monocle;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows an entity to apply a multiplier to the time rate without causing conflicts.
    /// </summary>
    [Tracked]
    public class TimeRateModifier : Component {

        public float Multiplier;
        public bool Enabled;
        
        public TimeRateModifier(float multiplier, bool enabled = true) : base(false, false) {
            Multiplier = multiplier;
            Enabled = enabled;
        }
    }
}