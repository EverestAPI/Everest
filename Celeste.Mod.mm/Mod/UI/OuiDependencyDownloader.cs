using Celeste.Mod.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    class OuiDependencyDownloader : OuiLoggedProgress {
        public static List<EverestModuleMetadata> MissingDependencies;

        public List<string> ModPathToLoad;
        private bool shouldAutoExit;
        private bool shouldRestart;

        private Everest.Updater.Entry everestVersionToInstall;

        private Task task = null;

        public override IEnumerator Enter(Oui from) {
            Everest.Loader.AutoLoadNewMods = false;
            Everest.Loader.OnCrawlMod += logCrawlMod;

            Title = Dialog.Clean("DEPENDENCYDOWNLOADER_TITLE");
            task = new Task(downloadAllDependencies);
            ModPathToLoad = new List<string>();
            Lines = new List<string>();
            Progress = 0;
            ProgressMax = 0;
            shouldAutoExit = true;
            shouldRestart = false;

            task.Start();

            return base.Enter(from);
        }

        public override IEnumerator Leave(Oui next) {
            Everest.Loader.AutoLoadNewMods = true;
            Everest.Loader.OnCrawlMod -= logCrawlMod;

            return base.Leave(next);
        }

        private void logCrawlMod(string filePath, EverestModuleMetadata meta) {
            if (meta != null) {
                LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_LOADING_MOD"), meta.ToString(), filePath));
            } else {
                LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_LOADING_MOD_NOMETA"), filePath));
            }
        }

        private void downloadAllDependencies() {
            // 1. Compute the list of dependencies we must download.
            LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_DOWNLOADING_DATABASE"));

            Everest.Updater.Entry everestVersionToInstall = null;

            Dictionary<string, ModUpdateInfo> availableDownloads = ModUpdaterHelper.DownloadModUpdateList();

            if (availableDownloads == null) {
                shouldAutoExit = false;
                shouldRestart = false;

                LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_DOWNLOAD_DATABASE_FAILED"));
            } else {
                // add transitive dependencies to the list of dependencies to download, by using the mod dependency graph.
                Dictionary<string, EverestModuleMetadata> modDependencyGraph = ModUpdaterHelper.DownloadModDependencyGraph();
                if (modDependencyGraph != null) {
                    addTransitiveDependencies(modDependencyGraph);
                }
                lock (Everest._Modules) {
                    if (Everest._Modules.SelectMany(mod => mod.Metadata.OptionalDependencies).Any(install => {
                        if (MissingDependencies.Any(todo => todo.Name == install.Name)) {
                            return true;
                        }
                        return false;
                    })) {
                        shouldRestart = true;
                    }
                }
                // load information on all installed mods, so that we can spot blacklisted ones easily.
                LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_LOADING_INSTALLED_MODS"));

                Progress = 0;
                ProgressMax = 100;
                Dictionary<string, EverestModuleMetadata[]> allModsInformationFlipped =
                    OuiModToggler.LoadAllModYamls(progress => {
                        Lines[Lines.Count - 1] = $"{Dialog.Clean("DEPENDENCYDOWNLOADER_LOADING_INSTALLED_MODS")} ({(int) (progress * 100)}%)";
                        Progress = (int) (progress * 100);
                    });
                ProgressMax = 0;

                // but flip around the mapping for convenience.
                Dictionary<EverestModuleMetadata, string> allModsInformation = new Dictionary<EverestModuleMetadata, string>();
                foreach (KeyValuePair<string, EverestModuleMetadata[]> mods in allModsInformationFlipped) {
                    foreach (EverestModuleMetadata mod in mods.Value) {
                        allModsInformation[mod] = mods.Key;
                    }
                }
                Lines[Lines.Count - 1] = $"{Dialog.Clean("DEPENDENCYDOWNLOADER_LOADING_INSTALLED_MODS")} {Dialog.Clean("DEPENDENCYDOWNLOADER_DONE")}";

                Logger.Verbose("OuiDependencyDownloader", "Computing dependencies to download...");

                // these mods are not installed currently, we will install them
                Dictionary<string, ModUpdateInfo> modsToInstall = new Dictionary<string, ModUpdateInfo>();

                // these mods are already installed, but need an update to satisfy the dependency
                Dictionary<string, ModUpdateInfo> modsToUpdate = new Dictionary<string, ModUpdateInfo>();
                Dictionary<string, EverestModuleMetadata> modsToUpdateCurrentVersions = new Dictionary<string, EverestModuleMetadata>();

                // Everest should be updated to satisfy a dependency on Everest
                bool shouldUpdateEverestManually = false;

                // these mods are absent from the database
                HashSet<string> modsNotFound = new HashSet<string>();

                // these mods have multiple downloads, and as such, should be installed manually
                HashSet<string> modsNotInstallableAutomatically = new HashSet<string>();

                // these mods should be unblacklisted.
                HashSet<string> modFilenamesToUnblacklist = new HashSet<string>();

                // these mods are in the database, but the version found in there won't satisfy the dependency
                Dictionary<string, HashSet<Version>> modsWithIncompatibleVersionInDatabase = new Dictionary<string, HashSet<Version>>();
                Dictionary<string, string> modsDatabaseVersions = new Dictionary<string, string>();

                foreach (EverestModuleMetadata dependency in MissingDependencies) {
                    if (Everest.Loader.Delayed.Any(delayedMod => dependency.Name == delayedMod.Item1.Name)) {
                        Logger.Verbose("OuiDependencyDownloader", $"{dependency.Name} is installed but load is delayed, skipping");

                    } else if (dependency.Name == "Everest" || dependency.Name == Core.CoreModule.NETCoreMetaName) {
                        Logger.Verbose("OuiDependencyDownloader", $"Everest should be updated");
                        shouldAutoExit = false;

                        if (dependency.Version.Major != 1 || dependency.Version.Build > 0 || dependency.Version.Revision > 0) {
                            // the Everest version is not 1.XXX.0.0: Everest should be updated manually because this shouldn't happen.
                            shouldUpdateEverestManually = true;

                        } else if (!shouldUpdateEverestManually && (everestVersionToInstall == null || everestVersionToInstall.Build < dependency.Version.Minor)) {
                            everestVersionToInstall = findEverestVersionToInstall(dependency.Version.Minor);
                            if (everestVersionToInstall == null) {
                                // a suitable version was not found! so, it should be installed manually.
                                shouldUpdateEverestManually = true;
                            }
                        }
                    } else if (tryUnblacklist(dependency, allModsInformation, modFilenamesToUnblacklist)) {
                        Logger.Verbose("OuiDependencyDownloader", $"{dependency.Name} is blacklisted, and should be unblacklisted instead");

                    } else if (!availableDownloads.ContainsKey(dependency.Name)) {
                        Logger.Verbose("OuiDependencyDownloader", $"{dependency.Name} was not found in the database");
                        modsNotFound.Add(dependency.Name);
                        shouldAutoExit = false;

                    } else if (availableDownloads[dependency.Name].xxHash.Count > 1) {
                        Logger.Verbose("OuiDependencyDownloader", $"{dependency.Name} has multiple versions and cannot be installed automatically");
                        modsNotInstallableAutomatically.Add(dependency.Name);
                        shouldAutoExit = false;

                    } else if (!isVersionCompatible(dependency.Version, availableDownloads[dependency.Name].Version)) {
                        Logger.Verbose("OuiDependencyDownloader", $"{dependency.Name} has a version in database ({availableDownloads[dependency.Name].Version}) that would not satisfy dependency ({dependency.Version})");

                        // add the required version to the list of versions for this mod
                        HashSet<Version> requiredVersions = modsWithIncompatibleVersionInDatabase.TryGetValue(dependency.Name, out HashSet<Version> result) ? result : new HashSet<Version>();
                        requiredVersions.Add(dependency.Version);
                        modsWithIncompatibleVersionInDatabase[dependency.Name] = requiredVersions;
                        modsDatabaseVersions[dependency.Name] = availableDownloads[dependency.Name].Version;
                        shouldAutoExit = false;

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
                            Logger.Verbose("OuiDependencyDownloader", $"{dependency.Name} is already installed and will be updated");
                            modsToUpdate[dependency.Name] = availableDownloads[dependency.Name];
                            modsToUpdateCurrentVersions[dependency.Name] = installedVersion;

                        } else {
                            Logger.Verbose("OuiDependencyDownloader", $"{dependency.Name} will be installed");
                            modsToInstall[dependency.Name] = availableDownloads[dependency.Name];
                        }
                    }
                }

                // actually install the mods now
                foreach (ModUpdateInfo modToInstall in modsToInstall.Values)
                    downloadDependency(modToInstall, null);

                foreach (ModUpdateInfo modToUpdate in modsToUpdate.Values)
                    downloadDependency(modToUpdate, modsToUpdateCurrentVersions[modToUpdate.Name]);

                // unblacklist mods if this is needed
                if (modFilenamesToUnblacklist.Count > 0) {
                    // remove the mods from blacklist.txt
                    if (!unblacklistMods(modFilenamesToUnblacklist)) {
                        // something bad happened
                        shouldAutoExit = false;
                        shouldRestart = true;
                    }

                    foreach (string modFilename in modFilenamesToUnblacklist) {
                        try {
                            LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_MOD_UNBLACKLIST"), modFilename));

                            // remove the mod from the loaded blacklist
                            Everest.Loader._Blacklist.RemoveWhere(item => item == modFilename);

                            // hot load the mod
                            string path = Path.Combine(Everest.Loader.PathMods, modFilename);
                            ModPathToLoad.Add(path);
                        } catch (Exception e) {
                            // something bad happened during the mod hot loading, log it and prompt to restart the game to load the mod.
                            LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_UNBLACKLIST_FAILED"));
                            Logger.LogDetailed(e);
                            shouldRestart = true;
                            break;
                        }
                    }
                }

                // don't waste time hot loading since we're restarting anyway
                if (!shouldRestart && everestVersionToInstall == null) {
                    foreach (string path in ModPathToLoad) {
                        try {
                            if (File.Exists(path)) {
                                Everest.Loader.LoadZip(path);
                            } else {
                                Everest.Loader.LoadDir(path);
                            }
                        } catch (Exception e) {
                            // something bad happened during the mod hot loading, log it and prompt to restart the game to load the mod.
                            LogLine(string.Format(Dialog.Clean("DEPENDENCYDOWNLOADER_HOTLOAD_FAILED"), Path.GetFileName(path)));
                            Logger.LogDetailed(e);
                            shouldRestart = true;
                            break;
                        }
                    }
                }

                // display all mods that couldn't be accounted for
                if (shouldUpdateEverestManually)
                    LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_MUST_UPDATE_EVEREST"));

                foreach (string mod in modsNotFound) {
                    if (mod == "Celeste") {
                        // "some of your mods require a more recent version of Celeste"
                        LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_UPDATE_CELESTE"));
                    } else {
                        // "xx could not be found in the database"
                        LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_MOD_NOT_FOUND"), mod));
                    }
                }

                foreach (string mod in modsNotInstallableAutomatically)
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_MOD_NOT_AUTO_INSTALLABLE"), mod));

                foreach (string mod in modsWithIncompatibleVersionInDatabase.Keys)
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_MOD_WRONG_VERSION"), mod,
                        string.Join(", ", modsWithIncompatibleVersionInDatabase[mod]), modsDatabaseVersions[mod]));
            }

            Progress = 1;
            ProgressMax = 1;

            if (shouldAutoExit) {
                // there are no errors to display: restart automatically
                if (shouldRestart) {
                    LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_RESTARTING"));
                    for (int i = 3; i > 0; --i) {
                        Lines[Lines.Count - 1] = string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_RESTARTING_IN"), i);
                        Thread.Sleep(1000);
                    }
                    Lines[Lines.Count - 1] = Dialog.Clean("DEPENDENCYDOWNLOADER_RESTARTING");

                    Everest.QuickFullRestart();

                } else {
                    Exit();
                }

            } else if (everestVersionToInstall != null) {
                LogLine("\n" + string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_EVEREST_UPDATE"), everestVersionToInstall.Build));
                this.everestVersionToInstall = everestVersionToInstall;

            } else if (shouldRestart) {
                LogLine("\n" + Dialog.Clean("DEPENDENCYDOWNLOADER_PRESS_BACK_TO_RESTART"));

            } else {
                LogLine("\n" + Dialog.Clean("DEPENDENCYDOWNLOADER_PRESS_BACK_TO_GO_BACK"));
            }
        }

        class DependencyNode {
            public bool Optional;
            public EverestModuleMetadata metadata;
            public List<DependencyNode> Dependencies = new List<DependencyNode>();
            public int RefCount = 1;
            public DependencyNode(EverestModuleMetadata from, bool opt) {
                Optional = opt;
                metadata = from;
            }
        }
        private static void addTransitiveDependencies(Dictionary<string, EverestModuleMetadata> modDependencyGraph) {
            //Topological Sorting
            List<DependencyNode> missingDependencies = MissingDependencies.Select(x => new DependencyNode(x, false) { RefCount = 0, }).ToList();
            int index;
            Logger.Verbose("OuiDependencyDownloader", "Checking for transitive dependencies...");
            for (int i = 0; i < missingDependencies.Count; i++) {
                // all mods in fromt of i is
                // | optional -> no dependencies was added [a]
                // | not opt -> all dependencies was added [b]
                // 
                // all mods behind i is
                // | optional -> no dependencies was added [c]
                // | not opt -> no dependencies was added [d]

                DependencyNode metadatanode = missingDependencies[i];
                if (metadatanode.Optional) {
                    // we're moving [c] to [a]
                    // no-op
                    continue;
                }
                // we're moving [d] to [b]
                EverestModuleMetadata metadata = metadatanode.metadata;
                if (!modDependencyGraph.TryGetValue(metadata.Name, out EverestModuleMetadata graphEntry)) {
                    Logger.Verbose("OuiDependencyDownloader", $"{metadata.Name} was not found in the graph");
                } else {
                    foreach ((EverestModuleMetadata dependency, bool _opt) in graphEntry.Dependencies.Select(x => (x, false)).Concat(graphEntry.OptionalDependencies.Select(x => (x, true)))) {
                        bool opt = _opt;
                        bool has = Everest.Loader.TryGetDependencyIgnoreVersion(dependency, out EverestModule module);
                        bool version = has && Everest.Loader.VersionSatisfiesDependency(dependency.Version, module.Metadata.Version);
                        bool loaded = false;
                        if (opt && has) {
                            opt = false;
                        }
                        if (has && version) {
                            loaded = true;
                        }
                        //if (opt) {
                        //    loaded = false;
                        //}
                        if (loaded) {
                            // it's already loaded, so we can ignore it.
                            // if other mods are not satisfied with its version, we need reboot.
                            // in this case, it's not important if it's not ordered.
                            Logger.Verbose("OuiDependencyDownloader", $"{dependency.Name} is loaded");
                        } else if ((index = missingDependencies.FindIndex(dep => dep.metadata.Name == dependency.Name)) > 0) {
                            DependencyNode found = missingDependencies[index];
                            found.RefCount++;
                            Logger.Verbose("OuiDependencyDownloader", $"{dependency.Name} is already missing");
                            if (!Everest.Loader.VersionSatisfiesDependency(dependency.Version, found.metadata.Version)) {
                                Logger.Verbose("OuiDependencyDownloader", $"but is outdated.");
                                found.metadata = dependency;
                            }
                            if (found.Optional && !opt) {
                                found.Optional = false;
                                if (index < i) {
                                    // [a] -> [b]
                                    // actually it's [a] -> [d]
                                    missingDependencies.RemoveAt(index);
                                    missingDependencies.Add(found);
                                    i--;
                                } // else { [c] -> [d] nop }
                            }
                            metadatanode.Dependencies.Add(found);
                        } else {
                            EverestModuleMetadata toadd = dependency;
                            Logger.Verbose("OuiDependencyDownloader", $"{toadd.Name} was added to the missing dependencies!");
                            DependencyNode item = new DependencyNode(toadd, opt);
                            missingDependencies.Add(item);
                            metadatanode.Dependencies.Add(item);
                        }
                    }
                }
            }
            List<EverestModuleMetadata> Result = new List<EverestModuleMetadata>();
            List<DependencyNode> Prepared = missingDependencies.Where(x => x.RefCount == 0).ToList();
            for (int i = 0; i < Prepared.Count; i++) {
                DependencyNode metadatanode = Prepared[i];
                if (metadatanode.Optional) {
                    continue;
                }
                Result.Add(metadatanode.metadata);
                foreach (DependencyNode dep in metadatanode.Dependencies) {
                    dep.RefCount--;
                    if (dep.RefCount == 0) {
                        Prepared.Add(dep);
                    }
                }
            }
            if (Prepared.Count != missingDependencies.Count) {
                Logger.Verbose("OuiDependencyDownloader", $"WA");
            }
            MissingDependencies = Result;

            MissingDependencies.Reverse();
        }

        private static bool tryUnblacklist(EverestModuleMetadata dependency, Dictionary<EverestModuleMetadata, string> allModsInformation, HashSet<string> modsToUnblacklist) {
            KeyValuePair<EverestModuleMetadata, string> match = default;

            // let's find the most recent installed mod that has the required name.
            foreach (KeyValuePair<EverestModuleMetadata, string> candidate in allModsInformation) {
                if (dependency.Name == candidate.Key.Name && (match.Key == null || match.Key.Version < candidate.Key.Version)) {
                    match = candidate;
                }
            }

            if (match.Key == null || !Everest.Loader.Blacklist.Contains(match.Value)) {
                // no result for this dependency (it isn't actually installed) or the mod isn't blacklisted (so it failed loading for some other reason).
                return false;
            }

            if (modsToUnblacklist.Contains(match.Value)) {
                // STOP RIGHT HERE! we already are planning to unblacklist this dependency. No need to go further!
                return true;
            }

            // this dependency will have to be unblacklisted.
            modsToUnblacklist.Add(match.Value);

            // unblacklist all dependencies for this dependency. if one of them isn't unblacklistable, that doesn't matter: it will fail to load
            // after restarting the game and it will be handled then (this case should be extremely rare anyway).
            foreach (EverestModuleMetadata dependencyDependency in match.Key.Dependencies) {
                if (!Everest.Loader.DependencyLoaded(dependencyDependency)) {
                    tryUnblacklist(dependencyDependency, allModsInformation, modsToUnblacklist);
                }
            }

            // and we are done!
            return true;
        }

        private bool unblacklistMods(HashSet<string> modFilenamesToUnblacklist) {
            try {
                // read the current blacklist file, changing nothing except for trimming lines.
                List<string> currentBlacklist = File.ReadAllLines(Everest.Loader.PathBlacklist).Select(l => l.Trim()).ToList();

                // start writing the new version.
                HashSet<string> modsLeftToUnblacklist = new HashSet<string>(modFilenamesToUnblacklist);
                using (StreamWriter blacklistTxt = File.CreateText(Everest.Loader.PathBlacklist)) {
                    foreach (string line in currentBlacklist) {
                        if (modFilenamesToUnblacklist.Contains(line)) {
                            // comment this line to unblacklist this mod.
                            blacklistTxt.WriteLine("# " + line);
                            modsLeftToUnblacklist.Remove(line);
                            Logger.Verbose("OuiDependencyDownloader", "Commented out line from blacklist.txt: " + line);
                        } else {
                            // copy the line as is.
                            blacklistTxt.WriteLine(line);
                        }
                    }
                }

                if (modsLeftToUnblacklist.Count > 0) {
                    // some mods we are supposed to unblacklist aren't in the blacklist.txt file...?
                    LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_UNBLACKLIST_FAILED"));
                    foreach (string mod in modsLeftToUnblacklist) {
                        Logger.Warn("OuiDependencyDownloader", "This mod could not be found in blacklist.txt: " + mod);
                    }
                    return false;
                }

                // everything went fine!
                return true;
            } catch (Exception e) {
                // something unexpected happened (a bug, or an I/O exception)
                LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_UNBLACKLIST_FAILED"));
                Logger.LogDetailed(e);
                return false;
            }
        }

        private bool isVersionCompatible(Version requiredVersion, string databaseVersionString) {
            // the update checker server does not perform any check on the version format, so be careful
            Version databaseVersion;
            try {
                databaseVersion = new Version(databaseVersionString);
            } catch (Exception e) {
                Logger.Warn("OuiDependencyDownloader", $"Could not parse version number: {databaseVersionString}");
                Logger.LogDetailed(e);
                return false;
            }

            return Everest.Loader.VersionSatisfiesDependency(requiredVersion, databaseVersion);
        }

        private Everest.Updater.Entry findEverestVersionToInstall(int requestedBuild) {
            // Find the entry with the highest build number and the highest priority
            Everest.Updater.UpdatePriority updatePrio = Everest.Updater.UpdatePriority.None;
            Everest.Updater.Entry updateEntry = null;

            foreach (Everest.Updater.Source source in Everest.Updater.Sources) {
                if (source?.Entries == null || source?.UpdatePriority is Everest.Updater.UpdatePriority.None)
                    continue;

                foreach (Everest.Updater.Entry entry in source.Entries) {
                    if (entry.Build < requestedBuild)
                        continue;

                    if (source.UpdatePriority < updatePrio)
                        continue;

                    if (source.UpdatePriority == updatePrio && entry.Build < updateEntry?.Build)
                        continue;

                    updatePrio = source.UpdatePriority;
                    updateEntry = entry;
                }
            }

            return updateEntry;
        }

        private void downloadDependency(ModUpdateInfo mod, EverestModuleMetadata installedVersion) {
            string downloadDestination = Path.Combine(Everest.PathTmp, $"dependency-download.zip");
            try {
                // 1. Download
                Func<int, long, int, bool> progressCallback = (position, length, speed) => {
                    if (length > 0) {
                        Lines[Lines.Count - 1] = $"{((int) Math.Floor(100D * (position / (double) length)))}% @ {speed} KiB/s";
                        Progress = position;
                        ProgressMax = (int) length;
                    } else {
                        Lines[Lines.Count - 1] = $"{((int) Math.Floor(position / 1000D))}KiB @ {speed} KiB/s";
                        ProgressMax = 0;
                    }
                    return true;
                };

                Exception downloadException = null;

                foreach (string url in ModUpdaterHelper.GetAllMirrorUrls(mod.URL)) {
                    try {
                        downloadException = null;
                        LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_DOWNLOADING"), mod.Name, url));
                        LogLine("", false);

                        Everest.Updater.DownloadFileWithProgress(url, downloadDestination, progressCallback);

                        ProgressMax = 0;
                        Lines[Lines.Count - 1] = Dialog.Clean("DEPENDENCYDOWNLOADER_DOWNLOAD_FINISHED");

                        // 2. Verify checksum
                        LogLine(Dialog.Clean("DEPENDENCYDOWNLOADER_VERIFYING_CHECKSUM"));
                        ModUpdaterHelper.VerifyChecksum(mod, downloadDestination);
                        break; // out of the loop
                    } catch (Exception e) when (e is WebException or TimeoutException or IOException) {
                        downloadException = e;
                        Logger.Warn("OuiDependencyDownloader", $"Download failed, trying another mirror");
                        Logger.LogDetailed(e);
                        Lines[Lines.Count - 1] = string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_DOWNLOADING_MIRROR"), "");
                        continue; // to the next mirror
                    }
                }

                if (downloadException != null) {
                    ModUpdaterHelper.TryDelete(downloadDestination);
                    throw downloadException;
                }

                // 3. Install mod
                if (installedVersion != null) {
                    shouldRestart = true;
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_UPDATING"), mod.Name, installedVersion.Version, mod.Version, installedVersion.PathArchive));
                    ModUpdaterHelper.InstallModUpdate(mod, installedVersion, downloadDestination);
                } else {
                    string installDestination = Path.Combine(Everest.Loader.PathMods, $"{mod.Name}.zip");
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_INSTALLING"), mod.Name, mod.Version, installDestination));
                    if (File.Exists(installDestination)) {
                        File.Delete(installDestination);
                    }
                    File.Move(downloadDestination, installDestination);
                    ModPathToLoad.Add(installDestination);
                }

            } catch (Exception e) {
                // install failed
                LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_INSTALL_FAILED"), mod.Name));
                Logger.LogDetailed(e);
                shouldAutoExit = false;

                // try to delete the file if it still exists.
                if (File.Exists(downloadDestination)) {
                    try {
                        Logger.Verbose("OuiDependencyDownloader", $"Deleting temp file {downloadDestination}");
                        File.Delete(downloadDestination);
                    } catch (Exception) {
                        Logger.Warn("OuiDependencyDownloader", $"Removing {downloadDestination} failed");
                    }
                }
            }
        }

        public override void Update() {
            // handle pressing Confirm to install a new Everest version
            if (everestVersionToInstall != null) {
                if (Input.MenuConfirm.Pressed && Focused) {
                    Everest.Updater.Update(OuiModOptions.Instance.Overworld.Goto<OuiLoggedProgress>(), everestVersionToInstall);
                }
            }
            // handle pressing the Back key
            else if (task != null && !shouldAutoExit && (task.IsCompleted || task.IsCanceled || task.IsFaulted) && Input.MenuCancel.Pressed && Focused) {
                if (shouldRestart) {
                    Everest.QuickFullRestart();
                } else {
                    Exit();
                }
            }

            base.Update();
        }

        public void Exit() {
            task = null;
            Lines.Clear();
            MainThreadHelper.Schedule(() => ((patch_OuiMainMenu) Overworld.GetUI<OuiMainMenu>())?.NeedsRebuild());
            Audio.Play(SFX.ui_main_button_back);
            Overworld.Goto<OuiModOptions>();
        }
    }
}
