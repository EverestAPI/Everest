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
            Title = Dialog.Clean("MODOPTIONS_COREMODULE_DOWNLOADDEPS").ToUpperInvariant();
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
            LogLine("Downloading mod database...");
            Dictionary<string, ModUpdateInfo> availableDownloads = ModUpdaterHelper.DownloadModUpdateList();
            if (availableDownloads == null) {
                shouldAutoRestart = false;
                shouldRestart = false;

                LogLine($"[ERROR] Downloading the database failed. Please check your log.txt for more info.");
            } else {
                LogLine("Computing dependencies to download...");
                Dictionary<string, ModUpdateInfo> modsToInstall = new Dictionary<string, ModUpdateInfo>();
                Dictionary<string, ModUpdateInfo> modsToUpdate = new Dictionary<string, ModUpdateInfo>();
                Dictionary<string, EverestModuleMetadata> modsToUpdateCurrentVersions = new Dictionary<string, EverestModuleMetadata>();
                HashSet<string> modsNotFound = new HashSet<string>();
                HashSet<string> modsNotInstallableAutomatically = new HashSet<string>();
                HashSet<string> modsBlacklisted = new HashSet<string>();
                bool shouldUpdateEverest = false;

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

                    } else {
                        EverestModuleMetadata installedVersion = null;
                        foreach (EverestModule module in Everest.Modules) {
                            // note: if the mod is installed, but not as a zip, this will be treated as a fresh install.
                            // this is fine since zips take the priority over directories.
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
                    // TODO: also check if version is actually compatible?
                }

                // actually install the mods now
                foreach (ModUpdateInfo modToInstall in modsToInstall.Values)
                    downloadDependency(modToInstall, null);

                foreach (ModUpdateInfo modToUpdate in modsToUpdate.Values)
                    downloadDependency(modToUpdate, modsToUpdateCurrentVersions[modToUpdate.Name]);

                // display all mods that couldn't be accounted for
                if (shouldUpdateEverest)
                    LogLine($"[ERROR] You must update Everest to play some of your mods.");

                foreach (string mod in modsNotFound)
                    LogLine($"[ERROR] {mod} could not be found in the database. Please install this mod manually.");

                foreach (string mod in modsNotInstallableAutomatically)
                    LogLine($"[ERROR] {mod} is available in multiple versions and cannot be installed automatically. Please install this mod manually.");

                foreach (string mod in modsBlacklisted)
                    LogLine($"[ERROR] {mod}.zip is present in your blacklist. Please unblacklist it to satisfy the dependency on {mod}.");
            }

            Progress = 1;
            ProgressMax = 1;

            if (shouldAutoRestart) {
                // there are no errors to display: restart automatically
                LogLine("Restarting");
                for (int i = 3; i > 0; --i) {
                    Lines[Lines.Count - 1] = $"Restarting in {i}";
                    Thread.Sleep(1000);
                }
                Lines[Lines.Count - 1] = $"Restarting";

                Everest.QuickFullRestart();
            } else if (shouldRestart) {
                LogLine("\nPress Back to restart Celeste.");
            } else {
                LogLine("\nPress Back to return to Mod Options.");
            }
        }

        private void downloadDependency(ModUpdateInfo mod, EverestModuleMetadata installedVersion) {
            string downloadDestination = Path.Combine(Everest.PathGame, $"dependency-download.zip");
            try {
                // 1. Download
                LogLine($"Downloading {mod.Name} from {mod.URL}...");
                LogLine($"", false);

                Everest.Updater.DownloadFileWithProgress(mod.URL, downloadDestination, (position, length, speed) => {
                    if (length > 0) {
                        Lines[Lines.Count - 1] = $"{((int)Math.Floor(100D * (position / (double)length)))}% @ {speed} KiB/s";
                        Progress = position;
                        ProgressMax = (int)length;
                    } else {
                        Lines[Lines.Count - 1] = $"{((int)Math.Floor(position / 1000D))}KiB @ {speed} KiB/s";
                        ProgressMax = 0;
                    }
                });

                ProgressMax = 0;
                Lines[Lines.Count - 1] = $"Download finished.";

                // 2. Verify checksum
                LogLine($"Verifying checksum...");
                ModUpdaterHelper.VerifyChecksum(mod, downloadDestination);

                // 3. Install mod
                shouldRestart = true;
                if (installedVersion != null) {
                    LogLine($"Installing update for {mod.Name} ({installedVersion.Version} -> {mod.Version}) to {installedVersion.PathArchive}...");
                    ModUpdaterHelper.InstallModUpdate(mod, installedVersion, downloadDestination);
                } else {
                    string installDestination = Path.Combine(Everest.Loader.PathMods, $"{mod.Name}.zip");
                    LogLine($"Installing mod {mod.Name} v.{mod.Version} to {installDestination}...");
                    File.Move(downloadDestination, installDestination);
                }
            } catch (Exception e) {
                // install failed
                LogLine($"[ERROR] Installing {mod.Name} failed. Please check your log.txt for more info.");
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
