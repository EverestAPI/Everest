using Celeste.Mod.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    class OuiDependencyDownloader : OuiLoggedProgress {
        public static List<EverestModuleMetadata> MissingDependencies;

        private bool shouldAutoRestart;
        private bool shouldRestart;

        private Task task = null;

        public override IEnumerator Enter(Oui from) {
            Title = Dialog.Clean("DEPENDENCYDOWNLOADER_TITLE");
            task = new Task(downloadAllDependencies);
            Lines = new List<string>();
            Progress = 0;
            ProgressMax = 0;
            shouldAutoRestart = true;
            shouldRestart = false;

            task.Start();

            IEnumerator enterBase = base.Enter(from);
            while (enterBase.MoveNext()) yield return enterBase.Current;
            yield break;
        }

        private void downloadAllDependencies() {
            // 1. Compute the list of dependencies we must download.
            LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_DOWNLOADING_DATABASE"));
            Dictionary<string, ModUpdateInfo> availableDownloads = ModUpdaterHelper.DownloadModUpdateList();
            if (availableDownloads == null) {
                shouldAutoRestart = false;
                shouldRestart = false;

                LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_DOWNLOAD_DATABASE_FAILED"));
            } else {
                Logger.Log("OuiDependencyDownloader", "Computing dependencies to download...");

                // these mods are not installed currently, we will install them
                Dictionary<string, ModUpdateInfo> modsToInstall = new Dictionary<string, ModUpdateInfo>();

                // these mods are already installed, but need an update to satisfy the dependency
                Dictionary<string, ModUpdateInfo> modsToUpdate = new Dictionary<string, ModUpdateInfo>();
                Dictionary<string, EverestModuleMetadata> modsToUpdateCurrentVersions = new Dictionary<string, EverestModuleMetadata>();

                // Everest should be updated to satisfy a dependency on Everest
                bool shouldUpdateEverest = false;

                // these mods are absent from the database
                HashSet<string> modsNotFound = new HashSet<string>();

                // these mods have multiple downloads, and as such, should be installed manually
                HashSet<string> modsNotInstallableAutomatically = new HashSet<string>();

                // these mods are blacklisted, and should be removed from the blacklist instead of being re-installed
                HashSet<string> modsBlacklisted = new HashSet<string>();

                // these mods are in the database, but the version found in there won't satisfy the dependency
                Dictionary<string, HashSet<Version>> modsWithIncompatibleVersionInDatabase = new Dictionary<string, HashSet<Version>>();
                Dictionary<string, string> modsDatabaseVersions = new Dictionary<string, string>();

                foreach (EverestModuleMetadata dependency in MissingDependencies) {
                    if (dependency.Name == "Everest") {
                        Logger.Log("OuiDependencyDownloader", $"Everest should be updated");
                        shouldUpdateEverest = true;
                        shouldAutoRestart = false;

                    // TODO: maybe check more precisely for blacklisted mods? We're only basing ourselves on the name here.
                    } else if (Everest.Loader.Blacklist.Contains($"{dependency.Name}.zip")) {
                        Logger.Log("OuiDependencyDownloader", $"{dependency.Name} is blacklisted, and should be unblacklisted instead");
                        modsBlacklisted.Add(dependency.Name);
                        shouldAutoRestart = false;

                    } else if (!availableDownloads.ContainsKey(dependency.Name)) {
                        Logger.Log("OuiDependencyDownloader", $"{dependency.Name} was not found in the database");
                        modsNotFound.Add(dependency.Name);
                        shouldAutoRestart = false;

                    } else if (availableDownloads[dependency.Name].xxHash.Count > 1) {
                        Logger.Log("OuiDependencyDownloader", $"{dependency.Name} has multiple versions and cannot be installed automatically");
                        modsNotInstallableAutomatically.Add(dependency.Name);
                        shouldAutoRestart = false;

                    } else if (!isVersionCompatible(dependency.Version, availableDownloads[dependency.Name].Version)) {
                        Logger.Log("OuiDependencyDownloader", $"{dependency.Name} has a version in database ({availableDownloads[dependency.Name].Version}) that would not satisfy dependency ({dependency.Version})");

                        // add the required version to the list of versions for this mod
                        HashSet<Version> requiredVersions = modsWithIncompatibleVersionInDatabase.TryGetValue(dependency.Name, out HashSet<Version> result) ? result : new HashSet<Version>();
                        requiredVersions.Add(dependency.Version);
                        modsWithIncompatibleVersionInDatabase[dependency.Name] = requiredVersions;
                        modsDatabaseVersions[dependency.Name] = availableDownloads[dependency.Name].Version;
                        shouldAutoRestart = false;

                    } else {
                        EverestModuleMetadata installedVersion = null;
                        foreach (EverestModule module in Everest.Modules) {
                            // note: if the mod is installed, but not as a zip, this will be treated as a fresh install rather than an update.
                            // this is fine since zips take the priority over directories, and we cannot update directory mods anyway.
                            if (module.Metadata.PathArchive != null && module.Metadata.Name == dependency.Name) {
                                installedVersion = module.Metadata;
                                break;
                            }
                        }

                        if (installedVersion != null) {
                            Logger.Log("OuiDependencyDownloader", $"{dependency.Name} is already installed and will be updated");
                            modsToUpdate[dependency.Name] = availableDownloads[dependency.Name];
                            modsToUpdateCurrentVersions[dependency.Name] = installedVersion;

                        } else {
                            Logger.Log("OuiDependencyDownloader", $"{dependency.Name} will be installed");
                            modsToInstall[dependency.Name] = availableDownloads[dependency.Name];
                        }
                    }
                }

                // actually install the mods now
                foreach (ModUpdateInfo modToInstall in modsToInstall.Values)
                    downloadDependency(modToInstall, null);

                foreach (ModUpdateInfo modToUpdate in modsToUpdate.Values)
                    downloadDependency(modToUpdate, modsToUpdateCurrentVersions[modToUpdate.Name]);

                // display all mods that couldn't be accounted for
                if (shouldUpdateEverest)
                    LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_MUST_UPDATE_EVEREST"));

                foreach (string mod in modsNotFound)
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_MOD_NOT_FOUND"), mod));

                foreach (string mod in modsNotInstallableAutomatically)
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_MOD_NOT_AUTO_INSTALLABLE"), mod));

                foreach (string mod in modsBlacklisted)
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_MOD_BLACKLISTED"), mod));

                foreach (string mod in modsWithIncompatibleVersionInDatabase.Keys)
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_MOD_WRONG_VERSION"), mod,
                        string.Join(", ", modsWithIncompatibleVersionInDatabase[mod]), modsDatabaseVersions[mod]));
            }

            Progress = 1;
            ProgressMax = 1;

            if (shouldAutoRestart) {
                // there are no errors to display: restart automatically
                LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_RESTARTING"));
                for (int i = 3; i > 0; --i) {
                    Lines[Lines.Count - 1] = string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_RESTARTING_IN"), i);
                    Thread.Sleep(1000);
                }
                Lines[Lines.Count - 1] = Dialog.Clean("DEPENDENCYDOWNLOADER_RESTARTING");

                Everest.QuickFullRestart();
            } else if (shouldRestart) {
                LogLine("\n" + Dialog.Clean("DEPENDENCYDOWNLOADER_PRESS_BACK_TO_RESTART"));
            } else {
                LogLine("\n" + Dialog.Clean("DEPENDENCYDOWNLOADER_PRESS_BACK_TO_GO_BACK"));
            }
        }

        private bool isVersionCompatible(Version requiredVersion, string databaseVersionString) {
            // the update checker server does not perform any check on the version format, so be careful
            Version databaseVersion;
            try {
                databaseVersion = new Version(databaseVersionString);
            } catch (Exception e) {
                Logger.Log("OuiDependencyDownloader", $"Could not parse version number: {databaseVersionString}");
                Logger.LogDetailed(e);
                return false;
            }

            return Everest.Loader.VersionSatisfiesDependency(requiredVersion, databaseVersion);
        }

        private void downloadDependency(ModUpdateInfo mod, EverestModuleMetadata installedVersion) {
            string downloadDestination = Path.Combine(Everest.PathGame, $"dependency-download.zip");
            try {
                // 1. Download
                LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_DOWNLOADING"), mod.Name, mod.URL));
                LogLine("", false);

                Everest.Updater.DownloadFileWithProgress(mod.URL, downloadDestination, (position, length, speed) => {
                    if (length > 0) {
                        Lines[Lines.Count - 1] = $"{((int) Math.Floor(100D * (position / (double) length)))}% @ {speed} KiB/s";
                        Progress = position;
                        ProgressMax = (int) length;
                    } else {
                        Lines[Lines.Count - 1] = $"{((int) Math.Floor(position / 1000D))}KiB @ {speed} KiB/s";
                        ProgressMax = 0;
                    }
                });

                ProgressMax = 0;
                Lines[Lines.Count - 1] = Dialog.Clean("DEPENDENCYDOWNLOADER_DOWNLOAD_FINISHED");

                // 2. Verify checksum
                LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_VERIFYING_CHECKSUM"));
                ModUpdaterHelper.VerifyChecksum(mod, downloadDestination);

                // 3. Install mod
                shouldRestart = true;
                if (installedVersion != null) {
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_UPDATING"), mod.Name, installedVersion.Version, mod.Version, installedVersion.PathArchive));
                    ModUpdaterHelper.InstallModUpdate(mod, installedVersion, downloadDestination);
                } else {
                    string installDestination = Path.Combine(Everest.Loader.PathMods, $"{mod.Name}.zip");
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_INSTALLING"), mod.Name, mod.Version, installDestination));
                    File.Move(downloadDestination, installDestination);
                }
            } catch (Exception e) {
                // install failed
                LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_INSTALL_FAILED"), mod.Name));
                Logger.LogDetailed(e);
                shouldAutoRestart = false;

                // try to delete the file if it still exists.
                if (File.Exists(downloadDestination)) {
                    try {
                        Logger.Log("OuiDependencyDownloader", $"Deleting temp file {downloadDestination}");
                        File.Delete(downloadDestination);
                    } catch (Exception) {
                        Logger.Log("OuiDependencyDownloader", $"Removing {downloadDestination} failed");
                    }
                }
            }
        }

        public override void Update() {
            // handle pressing the Back key
            if (task != null && !shouldAutoRestart && (task.IsCompleted || task.IsCanceled || task.IsFaulted) && Input.MenuCancel.Pressed) {
                if (shouldRestart) {
                    Everest.QuickFullRestart();
                } else {
                    // go back to mod options instead
                    task = null;
                    Lines.Clear();
                    Audio.Play(SFX.ui_main_button_back);
                    Overworld.Goto<OuiModOptions>();
                }
            }

            base.Update();
        }
    }
}
