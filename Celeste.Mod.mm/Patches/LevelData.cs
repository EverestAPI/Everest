#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;

namespace Celeste {
    public class patch_LevelData : LevelData {

        public patch_LevelData(BinaryPacker.Element data) : base(data) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchLevelDataBerryTracker]
        [MonoModConstructor]
        public extern void ctor(BinaryPacker.Element data);

    }
}