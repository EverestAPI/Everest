#pragma warning disable CS0649 // The field is never assigned and will always be null

namespace Celeste {
    class patch_DecalData : DecalData {

        public float Rotation;
        public string ColorHex;
        public int? Depth;

        public int GetDepth(int fallback) {
            return Depth ?? fallback;
        }

        public bool HasDepth() {
            return Depth.HasValue;
        }

    }
}
