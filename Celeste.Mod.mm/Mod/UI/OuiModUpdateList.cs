﻿using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    class OuiModUpdateList : Oui, OuiModOptions.ISubmenu {

        private TextMenu menu;
        private TextMenuExt.SubHeaderExt subHeader;
        private TextMenu.Button fetchingButton;
        private TextMenu.Button updateAllButton;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private Task task;

        private bool shouldRestart = false;

        private class CheckForUpdates {
            public Dictionary<string, ModUpdateInfo> updateCatalog = null;
            public SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdatesCatalog = new SortedDictionary<ModUpdateInfo, EverestModuleMetadata>();

            public void Fetch() {
                // 1. Download the mod updates database
                updateCatalog = ModUpdaterHelper.DownloadModUpdateList();

                // 2. Find out what actually has been updated
                if (updateCatalog != null) {
                    availableUpdatesCatalog = ModUpdaterHelper.ListAvailableUpdates(updateCatalog, excludeBlacklist: false);
                }
            }
        }

        private CheckForUpdates currentCheckForUpdates = null;
        private List<ModUpdateHolder> updatableMods = new List<ModUpdateHolder>();

        private static bool ongoingUpdateCancelled = false;
        private bool menuOnScreen = false;

        public override IEnumerator Enter(Oui from) {
            Everest.Loader.AutoLoadNewMods = false;

            menu = new TextMenu();

            // display the title and a dummy "Fetching" button
            menu.Add(new TextMenu.Header(Dialog.Clean("MODUPDATECHECKER_MENU_TITLE")));

            menu.Add(subHeader = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")));

            fetchingButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_FETCHING"));
            fetchingButton.Disabled = true;
            menu.Add(fetchingButton);

            Scene.Add(menu);

            currentCheckForUpdates = new CheckForUpdates();
            task = new Task(() => currentCheckForUpdates.Fetch());
            task.Start();

            menu.Visible = Visible = true;
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
            menuOnScreen = true;
        }

        public override IEnumerator Leave(Oui next) {
            Everest.Loader.AutoLoadNewMods = true;

            Audio.Play(SFX.ui_main_whoosh_large_out);
            menu.Focused = false;
            menuOnScreen = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;

            currentCheckForUpdates = null;
            updatableMods = new List<ModUpdateHolder>();

            task = null;
        }

        public override void Update() {
            // check if the "press Back to restart" message has to be toggled
            if (menu != null && subHeader != null) {
                if (menu.Focused && shouldRestart) {
                    subHeader.TextColor = Color.OrangeRed;
                    subHeader.Title = $"{Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")} ({Dialog.Clean("MODUPDATECHECKER_WILLRESTART")})";
                } else if (!menu.Focused && ongoingUpdateCancelled && menuOnScreen) {
                    subHeader.TextColor = Color.Gray;
                    subHeader.Title = $"{Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")} ({Dialog.Clean("MODUPDATECHECKER_CANCELLING")})";
                } else {
                    subHeader.TextColor = Color.Gray;
                    subHeader.Title = Dialog.Clean("MODUPDATECHECKER_MENU_HEADER");
                }
            }

            if (menu != null && task != null && task.IsCompleted) {
                // there is no download or install task in progress

                if (fetchingButton != null) {
                    // This means fetching the updates just finished. We have to remove the "Checking for updates" button
                    // and put the actual update list instead.

                    Logger.Log(LogLevel.Verbose, "OuiModUpdateList", "Rendering updates");

                    menu.Remove(fetchingButton);
                    fetchingButton = null;

                    if (currentCheckForUpdates.updateCatalog == null) {
                        // display an error message
                        TextMenu.Button button = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_ERROR"));
                        button.Disabled = true;
                        menu.Add(button);
                    } else if (currentCheckForUpdates.availableUpdatesCatalog.Count == 0) {
                        // display a dummy "no update available" button
                        TextMenu.Button button = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_NOUPDATE"));
                        button.Disabled = true;
                        menu.Add(button);
                    } else {
                        // if there are multiple updates...
                        if (currentCheckForUpdates.availableUpdatesCatalog.Count > 1) {
                            // display an "update all" button at the top of the list
                            updateAllButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_UPDATE_ALL"));
                            updateAllButton.Pressed(() => downloadAllMods());

                            menu.Add(updateAllButton);
                        }

                        // then, display one button per update
                        foreach (ModUpdateInfo update in currentCheckForUpdates.availableUpdatesCatalog.Keys) {
                            EverestModuleMetadata metadata = currentCheckForUpdates.availableUpdatesCatalog[update];

                            string versionUpdate = metadata.VersionString;
                            if (metadata.VersionString != update.Version)
                                versionUpdate = $"{metadata.VersionString} > {update.Version}";

                            TextMenu.Button button = new TextMenu.Button($"{ModUpdaterHelper.FormatModName(metadata.Name)} | v. {versionUpdate} ({new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(update.LastUpdate):yyyy-MM-dd})");
                            button.Pressed(() => {
                                // make the menu non-interactive
                                menu.Focused = false;
                                button.Disabled = true;

                                // trigger the update download
                                downloadModUpdate(update, metadata, button);
                            });

                            // if there is more than one hash, it means there is multiple downloads for this mod. Thus, we can't update it manually.
                            // if there isn't, add it to the list of mods that can be updated via "update all"
                            if (update.xxHash.Count > 1) {
                                button.Disabled = true;
                            } else {
                                updatableMods.Add(new ModUpdateHolder() { update = update, metadata = metadata, button = button });
                            }

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

            if (menuOnScreen && Input.MenuCancel.Pressed) {
                // cancel any ongoing download (this has no effect if no download is ongoing anyway).
                ongoingUpdateCancelled = true;

                if (menu != null && currentCheckForUpdates.updateCatalog == null && task != null && !task.IsCompleted) {
                    // cancelling out during check for updates: go back to mod options instead
                    Audio.Play(SFX.ui_main_button_back);
                    Overworld.Goto<OuiModOptions>();
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
        /// Downloads and installs a mod update.
        /// </summary>
        /// <param name="update">The update info coming from the update server</param>
        /// <param name="mod">The mod metadata from Everest for the installed mod</param>
        /// <param name="button">The button for that mod shown on the interface</param>
        private void downloadModUpdate(ModUpdateInfo update, EverestModuleMetadata mod, TextMenu.Button button) {
            task = new Task(() => {
                bool updateSuccess = doDownloadModUpdate(update, mod, button);

                if (updateSuccess) {
                    // select another enabled option: the next one, or the last one if there is no next one.
                    if (menu.Selection + 1 > menu.LastPossibleSelection)
                        menu.Selection = menu.LastPossibleSelection;
                    else
                        menu.MoveSelection(1);

                    // remove this mod from the updatable mods list (it won't be updated by the "update all mods" button)
                    updatableMods.Remove(new ModUpdateHolder() { update = update, metadata = mod, button = button });
                } else {
                    // re-enable the button to allow the user to try again.
                    button.Disabled = false;
                }

                // give the menu control back to the player
                menu.Focused = true;
            });

            task.Start();
        }

        /// <summary>
        /// Does the actual downloading of the mod. This is it's own function, to avoid double code
        /// </summary>
        /// <param name="update">The update info coming from the update server</param>
        /// <param name="mod">The mod metadata from Everest for the installed mod</param>
        /// <param name="button">The button for that mod shown on the interface</param>
        /// <returns>Bool wether the update failed or not</returns>
        private bool doDownloadModUpdate(ModUpdateInfo update, EverestModuleMetadata mod, TextMenu.Button button) {
            // first, reset the "cancelled" flag.
            ongoingUpdateCancelled = false;

            // we will download the mod to Celeste_Directory/[update.GetHashCode()].zip at first.
            string zipPath = Path.Combine(Everest.PathGame, $"modupdate-{update.GetHashCode()}.zip");

            try {
                // download it...
                button.Label = $"{ModUpdaterHelper.FormatModName(update.Name)} ({Dialog.Clean("MODUPDATECHECKER_DOWNLOADING")})";
                downloadMod(update, button, zipPath);

                if (ongoingUpdateCancelled) {
                    Logger.Log(LogLevel.Verbose, "OuiModUpdateList", "Update was cancelled");

                    // try to delete mod-update.zip if it still exists.
                    ModUpdaterHelper.TryDelete(zipPath);

                    // update was cancelled!
                    button.Label = $"{ModUpdaterHelper.FormatModName(update.Name)} ({Dialog.Clean("MODUPDATECHECKER_CANCELLED")})";
                    return false;
                }

                // verify its checksum
                ModUpdaterHelper.VerifyChecksum(update, zipPath);

                // mark restarting as required, as we will do weird stuff like closing zips afterwards.
                shouldRestart = true;

                // install it
                button.Label = $"{ModUpdaterHelper.FormatModName(update.Name)} ({Dialog.Clean("MODUPDATECHECKER_INSTALLING")})";
                ModUpdaterHelper.InstallModUpdate(update, mod, zipPath);

                // done!
                button.Label = $"{ModUpdaterHelper.FormatModName(update.Name)} ({Dialog.Clean("MODUPDATECHECKER_UPDATED")})";

                return true;
            } catch (Exception e) {
                // update failed
                button.Label = $"{ModUpdaterHelper.FormatModName(update.Name)} ({Dialog.Clean("MODUPDATECHECKER_FAILED")})";
                Logger.Log(LogLevel.Warn, "OuiModUpdateList", $"Updating {update.Name} failed");
                Logger.LogDetailed(e);

                // try to delete mod-update.zip if it still exists.
                ModUpdaterHelper.TryDelete(zipPath);
                return false;
            }
        }

        /// <summary>
        /// Downloads a mod update.
        /// </summary>
        /// <param name="update">The update info coming from the update server</param>
        /// <param name="button">The button for that mod shown on the interface</param>
        /// <param name="zipPath">The path to the zip the update will be downloaded to</param>
        private static void downloadMod(ModUpdateInfo update, TextMenu.Button button, string zipPath) {
            Logger.Log(LogLevel.Verbose, "OuiModUpdateList", $"Downloading {update.URL} to {zipPath}");

            Func<int, long, int, bool> progressCallback = (position, length, speed) => {
                if (ongoingUpdateCancelled) {
                    return false;
                }

                if (length > 0) {
                    button.Label = $"{ModUpdaterHelper.FormatModName(update.Name)} ({((int) Math.Floor(100D * (position / (double) length)))}% @ {speed} KiB/s)";
                } else {
                    button.Label = $"{ModUpdaterHelper.FormatModName(update.Name)} ({((int) Math.Floor(position / 1000D))}KiB @ {speed} KiB/s)";
                }
                return true;
            };

            try {
                Everest.Updater.DownloadFileWithProgress(update.URL, zipPath, progressCallback);
            } catch (WebException e) {
                Logger.Log(LogLevel.Warn, "OuiModUpdateList", $"Download failed, trying mirror {update.MirrorURL}");
                Logger.LogDetailed(e);
                Everest.Updater.DownloadFileWithProgress(update.MirrorURL, zipPath, progressCallback);
            }
        }

        /// <summary>
        /// Downloads all automatically updatable mods
        /// </summary>
        private void downloadAllMods() {
            task = new Task(() => {
                // make the menu non-interactive
                menu.Focused = false;
                updateAllButton.Disabled = true;
                updateAllButton.Label = Dialog.Clean("MODUPDATECHECKER_UPDATE_ALL_INPROGRESS");

                // disable all mod update buttons
                foreach (ModUpdateHolder modupdate in updatableMods) {
                    modupdate.button.Disabled = true;
                }

                menu.Focused = false;
                for (int i = 0; i < updatableMods.Count; i++) {
                    ModUpdateHolder modupdate = updatableMods[i];

                    // focus on the mod being updated, to ensure it is on-screen.
                    focusOn(modupdate.button);

                    if (doDownloadModUpdate(modupdate.update, modupdate.metadata, modupdate.button)) {
                        // if update is successful, remove this mod from the "update all" list
                        updatableMods.Remove(modupdate);
                        i--;
                    } else {
                        // stop trying to update further mods.
                        break;
                    }
                }

                // focus back on the "update all mods" button.
                focusOn(updateAllButton);

                // enable all (remaining) mod update buttons + the "update all" button if any mod is left to update
                foreach (ModUpdateHolder modupdate in updatableMods) {
                    modupdate.button.Disabled = false;
                }
                if (updatableMods.Count != 0) {
                    updateAllButton.Label = Dialog.Clean("MODUPDATECHECKER_UPDATE_ALL");
                    updateAllButton.Disabled = false;
                } else {
                    updateAllButton.Label = Dialog.Clean("MODUPDATECHECKER_UPDATE_ALL_DONE");
                }

                // give the menu control back to the player
                menu.Focused = true;
            });
            task.Start();
        }

        private void focusOn(TextMenu.Button button) {
            int index = menu.GetItems().IndexOf(button);
            if (index != -1) {
                menu.Selection = index;
            }
        }

        private struct ModUpdateHolder {
            public ModUpdateInfo update;
            public EverestModuleMetadata metadata;
            public TextMenu.Button button;
        }
    }
}
