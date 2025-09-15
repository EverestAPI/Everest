#pragma warning disable CS0649 // The field is never assigned and will always be null

using System;

namespace Celeste {
    class patch_DecalData : DecalData {

        public float Rotation;
        public string ColorHex;
        public int? Depth;
        public float? Parallax;

        // The following methods are used by MonoModRules.PatchLevelLoaderDecalCreation to avoid having to deal with generics

        [Obsolete("Everest implementation detail; should not be used by mods. Instead, directly use the Depth field.")]
        public bool HasDepth() => Depth.HasValue;
        [Obsolete("Everest implementation detail; should not be used by mods. Instead, directly use the Depth field.")]
        public int GetDepth(int fallback) => Depth ?? fallback;

        internal bool HasParallax() => Parallax.HasValue;
        internal float GetParallax() => Parallax ?? 0f;

    }
}
