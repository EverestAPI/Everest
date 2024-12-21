using Monocle;
using System.Collections.Generic;

namespace Celeste {
    class patch_SurfaceIndex: SurfaceIndex {

        public static Dictionary<int, string> IndexToCustomPath = new Dictionary<int, string>();

        public static Dictionary<int, int> IndexToSoundParam = new Dictionary<int, int>();

        public static Dictionary<int, ParticleType> IndexToDustParticles = new Dictionary<int, ParticleType>();

        public static Dictionary<ParticleType, float> DustParticlesToWallSlideOffsetX = new Dictionary<ParticleType, float>();

        public static string GetPathFromIndex(int key) {
            if (IndexToCustomPath.TryGetValue(key, out string path)) {
                return path;
            }
            return "event:/char/madeline";
        }

        public static int GetSoundParamFromIndex(int key) {
            if (IndexToSoundParam.TryGetValue(key, out int value)) {
                return value;
            }
            return key;
        }

        public static ParticleType GetDustParticleTypeFromIndex(int key) {
            if (IndexToDustParticles.TryGetValue(key, out ParticleType particles)) {
                return particles;
            }
            return null;
        }

    }
}
