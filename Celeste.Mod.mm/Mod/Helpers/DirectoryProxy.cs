using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Helpers {
    /// <summary>
    /// Partial replacement for the System.IO.Directory class.
    /// </summary>
    public static class DirectoryProxy {

        public static string[] GetFiles(string path) {
            string[] fs = Directory.GetFiles(path);

            if (!Everest.Content.TryGet<AssetTypeDirectory>(FileProxy._Modize(path), out ModAsset dir, true))
                return fs;

            lock (dir.Children) {
                return dir.Children.Select(
                    asset => Path.Combine(
                        path,
                        (asset.PathVirtual + "." + asset.Format)
                            .Substring(dir.PathVirtual.Length + 1)
                            .Replace('/', Path.DirectorySeparatorChar)
                    )
                ).Union(fs).ToArray();
            }
        }

    }
}
