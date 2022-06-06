using System.Collections.Generic;

namespace Celeste {
    class patch_SurfaceIndex: SurfaceIndex {

        public static Dictionary<int, string> IndexToCustomPath = new Dictionary<int, string>(); 

        public static string GetPathFromIndex(int key) {
            if (IndexToCustomPath.TryGetValue(key, out string path))
                return path;
            else
                return "event:/char/madeline";
        }

    }
}
