using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

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
                string modUpdaterDatabaseUrl = getModUpdaterDatabaseUrl();

                Logger.Log("ModUpdaterHelper", $"Downloading last versions list from {modUpdaterDatabaseUrl}");

                using (WebClient wc = new WebClient()) {
                    string yamlData = wc.DownloadString(modUpdaterDatabaseUrl);
                    updateCatalog = new Deserializer().Deserialize<Dictionary<string, ModUpdateInfo>>(yamlData);
                    foreach (string name in updateCatalog.Keys) {
                        updateCatalog[name].Name = name;
                    }
                    Logger.Log("ModUpdaterHelper", $"Downloaded {updateCatalog.Count} item(s)");
                }
            } catch (Exception e) {
                Logger.Log("ModUpdaterHelper", $"Downloading database failed!");
                Logger.LogDetailed(e);
            }

            return updateCatalog;
        }

        /// <summary>
        /// List all mods needing an update, by comparing the installed mods' hashes with the ones in the update checker database.
        /// </summary>
        /// <param name="updateCatalog">The update checker database (must not be null!)</param>
        /// <returns>A map listing all the updates: info from the update checker database => info from the installed mod</returns>
        public static SortedDictionary<ModUpdateInfo, EverestModuleMetadata> ListAvailableUpdates(Dictionary<string, ModUpdateInfo> updateCatalog) {
            SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdatesCatalog = new SortedDictionary<ModUpdateInfo, EverestModuleMetadata>(new MostRecentUpdatedFirst());

            Logger.Log("ModUpdaterHelper", "Checking for updates");

            foreach (EverestModule module in Everest.Modules) {
                EverestModuleMetadata metadata = module.Metadata;
                if (metadata.PathArchive != null && updateCatalog.ContainsKey(metadata.Name)) {
                    string xxHashStringInstalled = BitConverter.ToString(metadata.Hash).Replace("-", "").ToLowerInvariant();
                    Logger.Log("ModUpdaterHelper", $"Mod {metadata.Name}: installed hash {xxHashStringInstalled}, latest hash(es) {string.Join(", ", updateCatalog[metadata.Name].xxHash)}");
                    if (!updateCatalog[metadata.Name].xxHash.Contains(xxHashStringInstalled)) {
                        availableUpdatesCatalog.Add(updateCatalog[metadata.Name], metadata);
                    }
                }
            }

            Logger.Log("ModUpdaterHelper", $"{availableUpdatesCatalog.Count} update(s) available");
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
            Logger.Log("ModUpdaterHelper", $"Verifying checksum: actual hash is {actualHash}, expected hash is {expectedHash}");
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
                if (content.GetType() == typeof(ZipModContent) && (content as ZipModContent).Mod.Name == mod.Name) {
                    ZipModContent modZip = content as ZipModContent;

                    Logger.Log("ModUpdaterHelper", $"Closing mod .zip: {modZip.Path}");
                    modZip.Dispose();
                }
            }

            // delete the old zip, and move the new one.
            Logger.Log("ModUpdaterHelper", $"Deleting mod .zip: {mod.PathArchive}");
            File.Delete(mod.PathArchive);

            Logger.Log("ModUpdaterHelper", $"Moving {zipPath} to {mod.PathArchive}");
            File.Move(zipPath, mod.PathArchive);
        }

        /// <summary>
        /// Retrieves the mod updater database location from everestapi.github.io.
        /// This should point to a running instance of https://github.com/max4805/EverestUpdateCheckerServer.
        /// </summary>
        private static string getModUpdaterDatabaseUrl() {
            using (WebClient wc = new WebClient()) {
                Logger.Log("ModUpdaterHelper", "Fetching mod updater database URL");
                return wc.DownloadString("https://everestapi.github.io/modupdater.txt").Trim();
            }
        }

        private static Task updateCheckTask = null;
        private static SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdates = null;

        /// <summary>
        /// Run a check for mod updates asynchronously.
        /// </summary>
        public static void RunCheckForModUpdates() {
            updateCheckTask = new Task(() => {
                Dictionary<string, ModUpdateInfo> updateCatalog = DownloadModUpdateList();
                if (updateCatalog != null) {
                    availableUpdates = ListAvailableUpdates(updateCatalog);
                }
            });
            updateCheckTask.Start();
        }

        /// <summary>
        /// Returns the mod updates retrieved by RunCheckForModUpdates().
        /// Waits for the end of the task if it is not over yet.
        /// </summary>
        public static SortedDictionary<ModUpdateInfo, EverestModuleMetadata> GetLoadedModUpdates() {
            if (updateCheckTask != null)
                updateCheckTask.Wait();

            return availableUpdates;
        }
    }
}
