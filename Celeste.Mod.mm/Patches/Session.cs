#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;

namespace Celeste {
    public class patch_Session {
        public extern void orig_ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null);

        [MonoModConstructor]
        public void ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null) {
            AreaData.Get(area).TryOverrideMeta(area.Mode);
            orig_ctor(area, checkpoint, oldStats);
        }
    }
}