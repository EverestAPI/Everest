using Microsoft.Xna.Framework.Graphics;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Loader {

            public static string PathMods { get; internal set; }
            public static string PathCache { get; internal set; }

            public static string PathBlacklist { get; internal set; }
            internal static List<string> _Blacklist = new List<string>();
            public static ReadOnlyCollection<string> Blacklist => _Blacklist.AsReadOnly();

            internal static void LoadAuto() {
                Directory.CreateDirectory(PathMods = Path.Combine(PathGame, "Mods"));
                Directory.CreateDirectory(PathCache = Path.Combine(PathMods, "Cache"));

                PathBlacklist = Path.Combine(PathMods, "blacklist.txt");
                if (File.Exists(PathBlacklist)) {
                    _Blacklist = File.ReadAllLines(PathBlacklist).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
                } else {
                    using (StreamWriter writer = File.CreateText(PathBlacklist)) {
                        writer.WriteLine("# This is the blacklist. Lines starting with # are ignored.");
                        writer.WriteLine("ExampleFolder");
                        writer.WriteLine("SomeMod.zip");
                    }
                }

                string[] files = Directory.GetFiles(PathMods);
                for (int i = 0; i < files.Length; i++) {
                    string file = Path.GetFileName(files[i]);
                    if (!file.EndsWith(".zip") || _Blacklist.Contains(file))
                        continue;
                    LoadZip(file);
                }
                files = Directory.GetDirectories(PathMods);
                for (int i = 0; i < files.Length; i++) {
                    string file = Path.GetFileName(files[i]);
                    if (file == "Cache" || _Blacklist.Contains(file))
                        continue;
                    LoadDir(file);
                }

            }

            public static void LoadZip(string archive) {
                if (!File.Exists(archive)) // Relative path?
                    archive = Path.Combine(PathMods, archive);
                if (!File.Exists(archive)) // It just doesn't exist.
                    return;

                Logger.Log("loader", $"Loading mod .zip: {archive}");

                EverestModuleMetadata meta = null;
                Assembly asm = null;

                using (Stream zipStream = File.OpenRead(archive))
                using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
                    // In case the icon appears before the metadata in the .zip, store it temporarily.
                    Texture2D icon = null;

                    // First read the metadata, ...
                    foreach (ZipArchiveEntry entry in zip.Entries) {
                        if (entry.FullName == "metadata.yaml") {
                            using (Stream stream = entry.Open())
                            using (StreamReader reader = new StreamReader(stream))
                                meta = EverestModuleMetadata.Parse(archive, "", reader);
                            continue;
                        }
                        if (entry.FullName == "icon.png") {
                            using (Stream stream = entry.Open())
                                icon = Texture2D.FromStream(Celeste.Instance.GraphicsDevice, stream);
                            continue;
                        }
                    }

                    if (meta != null) {
                        if (icon != null)
                            meta.Icon = icon;

                        // ... then check if the dependencies are loaded ...
                        // TODO: Enqueue the mod, reload it on Register of other mods, rechecking if deps loaded.
                        foreach (EverestModuleMetadata dep in meta.Dependencies)
                            if (!DependencyLoaded(dep)) {
                                Logger.Log("loader", $"Dependency {dep} of mod {meta} not loaded!");
                                return;
                            }

                        // ... then add an AssemblyResolve handler for all the .zip-ped libraries
                        AppDomain.CurrentDomain.AssemblyResolve += GenerateModAssemblyResolver(meta);
                    }

                    // ... then handle the assembly ...
                    foreach (ZipArchiveEntry entry in zip.Entries) {
                        string entryName = entry.FullName.Replace('\\', '/');
                        if (meta != null && entryName == meta.DLL) {
                            using (MemoryStream ms = new MemoryStream()) {
                                using (Stream stream = entry.Open())
                                    stream.CopyTo(ms);
                                ms.Seek(0, SeekOrigin.Begin);
                                if (meta.Prelinked) {
                                    asm = Assembly.Load(ms.GetBuffer());
                                } else {
                                    asm = Relinker.GetRelinkedAssembly(meta, ms);
                                }
                            }
                        }
                    }

                    // ... then tell the Content class to crawl through the zip.
                    // (This also registers the zip for recrawls further down the line.)
                    Content.Crawl(null, archive, zip);
                }

                if (meta != null && asm != null)
                    LoadMod(meta, asm);
            }

            public static void LoadDir(string dir) {
                if (!Directory.Exists(dir)) // Relative path?
                    dir = Path.Combine(PathMods, dir);
                if (!Directory.Exists(dir)) // It just doesn't exist.
                    return;

                Logger.Log("loader", $"Loading mod directory: {dir}");

                EverestModuleMetadata meta = null;
                Assembly asm = null;

                // First read the metadata, ...
                string metaPath = Path.Combine(dir, "metadata.yaml");
                if (File.Exists(metaPath))
                    using (StreamReader reader = new StreamReader(metaPath))
                        meta = EverestModuleMetadata.Parse("", dir, reader);

                if (meta != null) {
                    // ... then check if the dependencies are loaded ...
                    foreach (EverestModuleMetadata dep in meta.Dependencies)
                        if (!DependencyLoaded(dep)) {
                            Logger.Log("loader", $"Dependency {dep} of mod {meta} not loaded!");
                            return;
                        }

                    // ... then add an AssemblyResolve handler for all the  .zip-ped libraries
                    AppDomain.CurrentDomain.AssemblyResolve += GenerateModAssemblyResolver(meta);
                }

                // ... then handle the assembly and all assets.
                Content.Crawl(null, dir);

                if (meta == null || !File.Exists(meta.DLL))
                    return;

                if (meta.Prelinked)
                    asm = Assembly.LoadFrom(meta.DLL);
                else
                    using (FileStream stream = File.OpenRead(meta.DLL))
                        asm = Relinker.GetRelinkedAssembly(meta, stream);

                if (asm != null)
                    LoadMod(meta, asm);
            }

            public static void LoadMod(EverestModuleMetadata meta, Assembly asm) {
                Content.Crawl(null, asm);

                Type[] types;
                try {
                    types = asm.GetTypes();
                } catch (Exception e) {
                    Logger.Log("loader", $"Failed reading assembly: {e}");
                    e.LogDetailed();
                    return;
                }
                for (int i = 0; i < types.Length; i++) {
                    Type type = types[i];
                    if (!typeof(EverestModule).IsAssignableFrom(type) || type.IsAbstract)
                        continue;

                    EverestModule mod = (EverestModule) type.GetConstructor(_EmptyTypeArray).Invoke(_EmptyObjectArray);
                    mod.Metadata = meta;
                    mod.Register();
                }
            }

            /// <summary>
            /// Checks if an dependency is loaded.
            /// Can be used by mods manually to f.e. activate / disable functionality.
            /// </summary>
            /// <param name="dependency">Dependency to check for. Name and Version will be checked.</param>
            /// <returns></returns>
            public static bool DependencyLoaded(EverestModuleMetadata dep) {
                string depName = dep.Name;
                Version depVersion = dep.Version;

                foreach (EverestModule mod in _Modules) {
                    EverestModuleMetadata meta = mod.Metadata;
                    if (meta.Name != depName)
                        continue;
                    Version version = meta.Version;

                    // Special case: Always true if version == 0.0.*
                    if (version.Major == 0 && version.Minor == 0)
                        return true;
                    // Major version, breaking changes, must match.
                    if (version.Major != depVersion.Major)
                        return false;
                    // Minor version, non-breaking changes, installed can't be lower than what we depend on.
                    if (version.Minor < depVersion.Minor)
                        return false;
                    return true;
                }

                return false;
            }

            private static ResolveEventHandler GenerateModAssemblyResolver(EverestModuleMetadata meta) {
                if (!string.IsNullOrEmpty(meta.PathArchive)) {
                    return (sender, args) => {
                        string asmName = new AssemblyName(args.Name).Name + ".dll";
                        using (Stream zipStream = File.OpenRead(meta.PathArchive))
                        using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
                            foreach (ZipArchiveEntry entry in zip.Entries) {
                                if (entry.FullName != asmName)
                                    continue;
                                using (Stream stream = entry.Open())
                                using (MemoryStream ms = new MemoryStream()) {
                                    stream.CopyTo(ms);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    return Assembly.Load(ms.GetBuffer());
                                }
                            }
                        }
                        return null;
                    };
                }

                if (!string.IsNullOrEmpty(meta.PathDirectory)) {
                    return (sender, args) => {
                        string asmPath = Path.Combine(meta.PathDirectory, new AssemblyName(args.Name).Name + ".dll");
                        if (!File.Exists(asmPath))
                            return null;
                        return Assembly.LoadFrom(asmPath);
                    };
                }

                return null;
            }

        }
    }
}
