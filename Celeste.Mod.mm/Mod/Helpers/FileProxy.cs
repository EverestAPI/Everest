using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    /// <summary>
    /// Partial replacement for the System.IO.File class.
    /// </summary>
    public static class FileProxy {

        private static string _Modize(string path) {
            // Trim the file extension, as we don't store it in our mod content mappings.
            string dir = Path.GetDirectoryName(path);
            path = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(dir))
                path = Path.Combine(dir, path);

            // Make the path relative to /Content/
            if (path.StartsWith(Everest.Content.PathContentOrig)) {
                path = path.Substring(Everest.Content.PathContentOrig.Length + 1);
            }

            // We use / instead of \ in mod content paths.
            path = path.Replace('\\', '/');

            return path;
        }

        public static bool Exists(string path) {
            if (File.Exists(path))
                return true;

            path = _Modize(path);
            return Everest.Content.Get(path) != null;
        }

        public static FileStream OpenRead(string path) {
            AssetMetadata meta;
            if (Everest.Content.TryGet(_Modize(path), out meta))
                return new FileProxyStream(meta.Stream);

            return File.OpenRead(path);
        }

    }
}
