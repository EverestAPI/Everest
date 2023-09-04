using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Celeste.Mod.Helpers {
    public class ModUpdaterHelper {
        private class MostRecentUpdatedFirst : IComparer<ModUpdateInfo> {
            public int Compare(ModUpdateInfo x, ModUpdateInfo y) {
                if (x.LastUpdate != y.LastUpdate) {
                    return y.LastUpdate - x.LastUpdate;
                }
                // fall back to alphabetical order
                return x.Name.CompareTo(y.Name);
            }
        }

        /// <summary>
        /// Downloads the full update list from the update checker server.
        /// Returns null if the download fails for any reason.
        /// </summary>
        public static Dictionary<string, ModUpdateInfo> DownloadModUpdateList() {
            Dictionary<string, ModUpdateInfo> updateCatalog = null;

            try {
                string modUpdaterDatabaseUrl = getModUpdaterDatabaseUrl("modupdater");

                Logger.Verbose("ModUpdaterHelper", $"Downloading last versions list from {modUpdaterDatabaseUrl}");

                using (WebClient wc = new CompressedWebClient()) {
                    string yamlData = wc.DownloadString(modUpdaterDatabaseUrl);
                    updateCatalog = YamlHelper.Deserializer.Deserialize<Dictionary<string, ModUpdateInfo>>(yamlData);
                    foreach (string name in updateCatalog.Keys) {
                        updateCatalog[name].Name = name;
                    }
                    Logger.Verbose("ModUpdaterHelper", $"Downloaded {updateCatalog.Count} item(s)");
                }
            } catch (Exception e) {
                Logger.Warn("ModUpdaterHelper", $"Downloading database failed!");
                Logger.LogDetailed(e);
            }

            return updateCatalog;
        }

        private class DependencyGraphEntry {
            public List<ModUpdateInfo> Dependencies { get; set; }
            public List<ModUpdateInfo> OptionalDependencies { get; set; }
        }

        /// <summary>
        /// Downloads the mod dependency graph from the update checker server.
        /// Returns null if the download fails for any reason.
        /// </summary>
        public static Dictionary<string, EverestModuleMetadata> DownloadModDependencyGraph() {
            try {
                string modUpdaterDatabaseUrl = getModUpdaterDatabaseUrl("modgraph");

                Logger.Verbose("ModUpdaterHelper", $"Downloading mod dependency graph from {modUpdaterDatabaseUrl}");

                Dictionary<string, EverestModuleMetadata> dependencyGraph = new Dictionary<string, EverestModuleMetadata>();

                using (WebClient wc = new CompressedWebClient()) {
                    string yamlData = wc.DownloadString(modUpdaterDatabaseUrl);
                    Dictionary<string, DependencyGraphEntry> dependencyGraphUnparsed = YamlHelper.Deserializer.Deserialize<Dictionary<string, DependencyGraphEntry>>(yamlData);

                    foreach (KeyValuePair<string, DependencyGraphEntry> entry in dependencyGraphUnparsed) {
                        EverestModuleMetadata result = new EverestModuleMetadata { Name = entry.Key };

                        // ArgumentExceptions may happen if any of the dependencies have invalid version numbers.

                        foreach (ModUpdateInfo info in entry.Value.Dependencies) {
                            try {
                                result.Dependencies.Add(new EverestModuleMetadata { Name = info.Name, VersionString = info.Version });
                            } catch (ArgumentException) {
                                continue;
                            }
                        }

                        foreach (ModUpdateInfo info in entry.Value.OptionalDependencies) {
                            try {
                                result.OptionalDependencies.Add(new EverestModuleMetadata { Name = info.Name, VersionString = info.Version });
                            } catch (ArgumentException) {
                                continue;
                            }
                        }

                        dependencyGraph[entry.Key] = result;
                    }

                    Logger.Verbose("ModUpdaterHelper", $"Downloaded {dependencyGraph.Count} item(s)");
                    return dependencyGraph;
                }
            } catch (Exception e) {
                Logger.Warn("ModUpdaterHelper", $"Downloading dependency graph failed!");
                Logger.LogDetailed(e);
                return null;
            }
        }

        /// <summary>
        /// List all mods needing an update, by comparing the installed mods' hashes with the ones in the update checker database.
        /// </summary>
        /// <param name="updateCatalog">The update checker database (must not be null!)</param>
        /// <param name="excludeBlacklist">If mods present in updaterblacklist.txt should be excluded from the result</param>
        /// <returns>A map listing all the updates: info from the update checker database => info from the installed mod</returns>
        public static SortedDictionary<ModUpdateInfo, EverestModuleMetadata> ListAvailableUpdates(Dictionary<string, ModUpdateInfo> updateCatalog, bool excludeBlacklist) {
            SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdatesCatalog = new SortedDictionary<ModUpdateInfo, EverestModuleMetadata>(new MostRecentUpdatedFirst());

            Logger.Verbose("ModUpdaterHelper", "Checking for updates");

            foreach (EverestModule module in Everest.Modules) {
                EverestModuleMetadata metadata = module.Metadata;
                if (metadata.PathArchive != null && updateCatalog.ContainsKey(metadata.Name)
                    && (!excludeBlacklist || !Everest.Loader.UpdaterBlacklist.Any(path => Path.Combine(Everest.Loader.PathMods, path) == metadata.PathArchive))) {

                    string xxHashStringInstalled = BitConverter.ToString(metadata.Hash).Replace("-", "").ToLowerInvariant();
                    Logger.Verbose("ModUpdaterHelper", $"Mod {metadata.Name}: installed hash {xxHashStringInstalled}, latest hash(es) {string.Join(", ", updateCatalog[metadata.Name].xxHash)}");
                    if (!updateCatalog[metadata.Name].xxHash.Contains(xxHashStringInstalled)) {
                        availableUpdatesCatalog[updateCatalog[metadata.Name]] = metadata;
                    }
                }
            }

            Logger.Verbose("ModUpdaterHelper", $"{availableUpdatesCatalog.Count} update(s) available");
            return availableUpdatesCatalog;
        }

        /// <summary>
        /// Verifies the downloaded mod's checksum, and throws an IOException if it doesn't match the database one.
        /// </summary>
        /// <param name="update">The mod info from the database</param>
        /// <param name="filePath">The path to the file to check</param>
        public static void VerifyChecksum(ModUpdateInfo update, string filePath) {
            string actualHash = BitConverter.ToString(Everest.GetChecksum(filePath)).Replace("-", "").ToLowerInvariant();
            string expectedHash = update.xxHash[0];
            Logger.Verbose("ModUpdaterHelper", $"Verifying checksum: actual hash is {actualHash}, expected hash is {expectedHash}");
            if (expectedHash != actualHash) {
                throw new IOException($"Checksum error: expected {expectedHash}, got {actualHash}");
            }
        }

        /// <summary>
        /// Installs a mod update in the Mods directory once it has been downloaded.
        /// This method will replace the installed mod zip with the one that was just downloaded.
        /// </summary>
        /// <param name="update">The update info coming from the update server</param>
        /// <param name="mod">The mod metadata from Everest for the installed mod</param>
        /// <param name="zipPath">The path to the zip the update has been downloaded to</param>
        public static void InstallModUpdate(ModUpdateInfo update, EverestModuleMetadata mod, string zipPath) {
            // let's close the zip, as we will replace it now.
            foreach (ModContent content in Everest.Content.Mods) {
                if (content.GetType() == typeof(ZipModContent) && (content as ZipModContent).Path == mod.PathArchive) {
                    ZipModContent modZip = content as ZipModContent;

                    Logger.Verbose("ModUpdaterHelper", $"Closing mod .zip: {modZip.Path}");
                    modZip.Dispose();
                }
            }

            // delete the old zip, and move the new one.
            Logger.Verbose("ModUpdaterHelper", $"Deleting mod .zip: {mod.PathArchive}");
            File.Delete(mod.PathArchive);

            Logger.Verbose("ModUpdaterHelper", $"Moving {zipPath} to {mod.PathArchive}");
            File.Move(zipPath, mod.PathArchive);
        }

        /// <summary>
        /// Tries deleting a file if it exists.
        /// If deletion fails, an error is written to the log.
        /// </summary>
        /// <param name="path">The path to the file to delete</param>
        public static void TryDelete(string path) {
            if (File.Exists(path)) {
                try {
                    Logger.Verbose("ModUpdaterHelper", $"Deleting file {path}");
                    File.Delete(path);
                } catch (Exception) {
                    Logger.Warn("ModUpdaterHelper", $"Removing {path} failed");
                }
            }
        }

        /// <summary>
        /// Retrieves the mod updater database location from everestapi.github.io.
        /// This should point to a running instance of https://github.com/maddie480/EverestUpdateCheckerServer.
        /// </summary>
        private static string getModUpdaterDatabaseUrl(string database) {
            using (WebClient wc = new CompressedWebClient()) {
                Logger.Verbose("ModUpdaterHelper", "Fetching mod updater database URL");
                return wc.DownloadString("https://everestapi.github.io/" + database + ".txt").Trim();
            }
        }

        private static Task updateCheckTask = null;
        private static SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdates = null;

        /// <summary>
        /// Run a check for mod updates asynchronously.
        /// <param name="excludeBlacklist">If mods present in updaterblacklist.txt should be excluded from the result</param>
        /// </summary>
        public static void RunAsyncCheckForModUpdates(bool excludeBlacklist) {
            updateCheckTask = new Task(() => {
                Dictionary<string, ModUpdateInfo> updateCatalog = DownloadModUpdateList();
                if (updateCatalog != null) {
                    availableUpdates = ListAvailableUpdates(updateCatalog, excludeBlacklist);
                }
            });
            updateCheckTask.Start();
        }

        /// <summary>
        /// Returns true if update checking is done, false otherwise.
        /// </summary>
        public static bool IsAsyncUpdateCheckingDone() {
            return updateCheckTask == null || updateCheckTask.Status != TaskStatus.Running;
        }

        /// <summary>
        /// Returns the mod updates retrieved by RunCheckForModUpdates().
        /// Waits for the end of the task if it is not over yet.
        /// </summary>
        public static SortedDictionary<ModUpdateInfo, EverestModuleMetadata> GetAsyncLoadedModUpdates() {
            if (updateCheckTask != null)
                updateCheckTask.Wait();

            return availableUpdates;
        }

        public static string FormatModName(string modNameRaw) {
            if (modNameRaw == null)
                return null;

            string overrideName = $"modname_{modNameRaw.DialogKeyify()}".DialogCleanOrNull();
            return overrideName ?? modNameRaw.SpacedPascalCase();
        }
    }
}
