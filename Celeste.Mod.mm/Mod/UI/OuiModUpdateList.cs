using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.UI {
    class OuiModUpdateList : Oui {

        private class ModUpdateInfo {
            public virtual string Name { get; set; }
            public virtual string Version { get; set; }
            public virtual int LastUpdate { get; set; }
            public virtual string URL { get; set; }
            public virtual List<string> xxHash { get; set; }
        }

        private class MostRecentUpdatedFirst : IComparer<ModUpdateInfo> {
            public int Compare(ModUpdateInfo x, ModUpdateInfo y) {
                if (x.LastUpdate != y.LastUpdate) {
                    return y.LastUpdate - x.LastUpdate;
                }
                // fall back to alphabetical order
                return x.Name.CompareTo(y.Name);
            }
        }

        private TextMenu menu;
        private TextMenuExt.SubHeaderExt subHeader;
        private TextMenu.Button fetchingButton;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private Task task;

        private bool shouldRestart = false;

        private Dictionary<string, ModUpdateInfo> updateCatalog = null;
        private SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdatesCatalog = new SortedDictionary<ModUpdateInfo, EverestModuleMetadata>(new MostRecentUpdatedFirst());

        public override IEnumerator Enter(Oui from) {
            menu = new TextMenu();

            // display the title and a dummy "Fetching" button
            menu.Add(new TextMenu.Header(Dialog.Clean("MODUPDATECHECKER_MENU_TITLE")));

            menu.Add(subHeader = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")));

            fetchingButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_FETCHING"));
            fetchingButton.Disabled = true;
            menu.Add(fetchingButton);

            Scene.Add(menu);

            menu.Visible = Visible = true;
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;

            task = new Task(() => {
                try {
                    // 1. Download the updates list
                    string modUpdaterDatabaseUrl = getModUpdaterDatabaseUrl();

                    Logger.Log("OuiModUpdateList", $"Downloading last versions list from {modUpdaterDatabaseUrl}");

                    using (WebClient wc = new WebClient()) {
                        string yamlData = wc.DownloadString(modUpdaterDatabaseUrl);
                        updateCatalog = new Deserializer().Deserialize<Dictionary<string, ModUpdateInfo>>(yamlData);
                        foreach (string name in updateCatalog.Keys) {
                            updateCatalog[name].Name = name;
                        }
                        Logger.Log("OuiModUpdateList", $"Downloaded {updateCatalog.Count} item(s)");
                    }
                } catch (Exception e) {
                    Logger.Log("OuiModUpdateList", $"Downloading database failed!");
                    Logger.LogDetailed(e);
                }

                // 2. Find out what actually has been updated
                availableUpdatesCatalog.Clear();

                if (updateCatalog != null) {
                    Logger.Log("OuiModUpdateList", "Checking for updates");

                    foreach (EverestModule module in Everest.Modules) {
                        EverestModuleMetadata metadata = module.Metadata;
                        if (metadata.PathArchive != null && updateCatalog.ContainsKey(metadata.Name)) {
                            string xxHashStringInstalled = BitConverter.ToString(metadata.Hash).Replace("-", "").ToLowerInvariant();
                            Logger.Log("OuiModUpdateList", $"Mod {metadata.Name}: installed hash {xxHashStringInstalled}, latest hash(es) {string.Join(", ", updateCatalog[metadata.Name].xxHash)}");
                            if (!updateCatalog[metadata.Name].xxHash.Contains(xxHashStringInstalled)) {
                                availableUpdatesCatalog.Add(updateCatalog[metadata.Name], metadata);
                            }
                        }
                    }

                    Logger.Log("OuiModUpdateList", $"{availableUpdatesCatalog.Count} update(s) available");
                }
            });

            task.Start();
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play(SFX.ui_main_whoosh_large_out);
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;

            updateCatalog = null;
            availableUpdatesCatalog = new SortedDictionary<ModUpdateInfo, EverestModuleMetadata>(new MostRecentUpdatedFirst());
            task = null;
        }

        public override void Update() {
            if (menu != null && task != null && task.IsCompleted) {
                // there is no download or install task in progress

                if (fetchingButton != null) {
                    // This means fetching the updates just finished. We have to remove the "Checking for updates" button
                    // and put the actual update list instead.

                    Logger.Log("OuiModUpdateList", "Rendering updates");

                    menu.Remove(fetchingButton);
                    fetchingButton = null;

                    if (updateCatalog == null) {
                        // display an error message
                        TextMenu.Button button = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_ERROR"));
                        button.Disabled = true;
                        menu.Add(button);
                    } else if (availableUpdatesCatalog.Count == 0) {
                        // display a dummy "no update available" button
                        TextMenu.Button button = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_NOUPDATE"));
                        button.Disabled = true;
                        menu.Add(button);
                    } else {
                        // display one button per update
                        foreach (ModUpdateInfo update in availableUpdatesCatalog.Keys) {
                            EverestModuleMetadata metadata = availableUpdatesCatalog[update];
                            TextMenu.Button button = new TextMenu.Button($"{metadata.Name} | v. {metadata.VersionString} > {update.Version} ({new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(update.LastUpdate):yyyy-MM-dd})");
                            button.Pressed(() => {
                                // make the menu non-interactive
                                menu.Focused = false;
                                button.Disabled = true;

                                // trigger the update download
                                downloadModUpdate(update, metadata, button);
                            });

                            // if there is more than one hash, it means there is multiple downloads for this mod. Thus, we can't update it manually.
                            if (update.xxHash.Count > 1) button.Disabled = true;

                            menu.Add(button);
                        }
                    }
                }

                if (menu.Focused && Selected && Input.MenuCancel.Pressed) {
                    if (shouldRestart) {
                        Everest.QuickFullRestart();
                    } else {
                        // go back to mod options instead
                        Audio.Play(SFX.ui_main_button_back);
                        Overworld.Goto<OuiModOptions>();
                    }
                }
            }

            base.Update();
        }

        public override void Render() {
            if (alpha > 0f) {
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);
            }
            base.Render();
        }

        /// <summary>
        /// Retrieves the mod updater database location from everestapi.github.io.
        /// This should point to a running instance of https://github.com/max4805/EverestUpdateCheckerServer.
        /// </summary>
        private string getModUpdaterDatabaseUrl() {
            using (WebClient wc = new WebClient()) {
                Logger.Log("OuiModUpdateList", "Fetching mod updater database URL");
                return wc.DownloadString("https://everestapi.github.io/modupdater.txt").Trim();
            }
        }

        /// <summary>
        /// Downloads and installs a mod update.
        /// </summary>
        /// <param name="update">The update info coming from the update server</param>
        /// <param name="mod">The mod metadata from Everest for the installed mod</param>
        /// <param name="button">The button for that mod shown on the interface</param>
        private void downloadModUpdate(ModUpdateInfo update, EverestModuleMetadata mod, TextMenu.Button button) {
            task = new Task(() => {
                // we will download the mod to Celeste_Directory/mod-update.zip at first.
                string zipPath = Path.Combine(Everest.PathGame, "mod-update.zip");

                try {
                    // download it...
                    button.Label = $"{update.Name} ({Dialog.Clean("MODUPDATECHECKER_DOWNLOADING")})";
                    downloadMod(update, button, zipPath);

                    // verify its checksum
                    string actualHash = BitConverter.ToString(Everest.GetChecksum("mod-update.zip")).Replace("-", "").ToLowerInvariant();
                    string expectedHash = update.xxHash[0];
                    Logger.Log("OuiModUpdateList", $"Verifying checksum: actual hash is {actualHash}, expected hash is {expectedHash}");
                    if (expectedHash != actualHash) {
                        throw new IOException($"Checksum error: expected {expectedHash}, got {actualHash}");
                    }

                    // mark restarting as required, as we will do weird stuff like closing zips afterwards.
                    if (!shouldRestart) {
                        shouldRestart = true;
                        subHeader.TextColor = Color.OrangeRed;
                        subHeader.Title = $"{Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")} ({Dialog.Clean("MODUPDATECHECKER_WILLRESTART")})";
                    }

                    // install it
                    button.Label = $"{update.Name} ({Dialog.Clean("MODUPDATECHECKER_INSTALLING")})";
                    installMod(update, mod, zipPath);

                    // done!
                    button.Label = $"{update.Name} ({Dialog.Clean("MODUPDATECHECKER_UPDATED")})";

                    // select another enabled option: the next one, or the last one if there is no next one.
                    if (menu.Selection + 1 > menu.LastPossibleSelection)
                        menu.Selection = menu.LastPossibleSelection;
                    else
                        menu.MoveSelection(1);
                } catch (Exception e) {
                    // update failed
                    button.Label = $"{update.Name} ({Dialog.Clean("MODUPDATECHECKER_FAILED")})";
                    Logger.Log("OuiModUpdateList", $"Updating {update.Name} failed");
                    Logger.LogDetailed(e);
                    button.Disabled = false;

                    // try to delete mod-update.zip if it still exists.
                    if (File.Exists(zipPath)) {
                        try {
                            Logger.Log("OuiModUpdateList", $"Deleting temp file {zipPath}");
                            File.Delete(zipPath);
                        } catch (Exception) {
                            Logger.Log("OuiModUpdateList", $"Removing {zipPath} failed");
                        }
                    }
                }

                // give the menu control back to the player
                menu.Focused = true;
            });
            task.Start();
        }

        /// <summary>
        /// Downloads a mod update.
        /// </summary>
        /// <param name="update">The update info coming from the update server</param>
        /// <param name="button">The button for that mod shown on the interface</param>
        /// <param name="zipPath">The path to the zip the update will be downloaded to</param>
        private static void downloadMod(ModUpdateInfo update, TextMenu.Button button, string zipPath) {
            Logger.Log("OuiModUpdateList", $"Downloading {update.URL} to {zipPath}");

            Everest.Updater.DownloadFileWithProgress(update.URL, zipPath, (position, length, speed) => {
                if (length > 0) {
                    button.Label = $"{update.Name} ({((int)Math.Floor(100D * (position / (double)length)))}% @ {speed} KiB/s)";
                } else {
                    button.Label = $"{update.Name} ({((int)Math.Floor(position / 1000D))}KiB @ {speed} KiB/s)";
                }
            });
        }

        /// <summary>
        /// Installs a mod update in the Mods directory once it has been downloaded.
        /// This method will replace the installed mod zip with the one that was just downloaded.
        /// </summary>
        /// <param name="update">The update info coming from the update server</param>
        /// <param name="mod">The mod metadata from Everest for the installed mod</param>
        /// <param name="zipPath">The path to the zip the update has been downloaded to</param>
        private static void installMod(ModUpdateInfo update, EverestModuleMetadata mod, string zipPath) {
            // let's close the zip, as we will replace it now.
            foreach (ModContent content in Everest.Content.Mods) {
                if (content.GetType() == typeof(ZipModContent) && (content as ZipModContent).Mod.Name == mod.Name) {
                    ZipModContent modZip = content as ZipModContent;

                    Logger.Log("OuiModUpdateList", $"Closing mod .zip: {modZip.Path}");
                    modZip.Dispose();
                }
            }

            // delete the old zip, and move the new one.
            Logger.Log("OuiModUpdateList", $"Deleting mod .zip: {mod.PathArchive}");
            File.Delete(mod.PathArchive);

            Logger.Log("OuiModUpdateList", $"Moving {zipPath} to {mod.PathArchive}");
            File.Move(zipPath, mod.PathArchive);
        }
    }
}
