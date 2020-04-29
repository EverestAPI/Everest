using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private bool willRestartMessageShown = false;

        private Dictionary<string, ModUpdateInfo> updateCatalog = null;
        private SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdatesCatalog = new SortedDictionary<ModUpdateInfo, EverestModuleMetadata>();
        private List<ModUpdateHolder> updatableMods = new List<ModUpdateHolder>();

        private static bool ongoingUpdateCancelled = false;

        public override IEnumerator Enter(Oui from) {
            Everest.Loader.AutoLoadNewMods = false;

            menu = new TextMenu();

            // display the title and a dummy "Fetching" button
            menu.Add(new TextMenu.Header(Dialog.Clean("MODUPDATECHECKER_MENU_TITLE")));

            menu.Add(subHeader = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")));
            willRestartMessageShown = false;

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
                // 1. Download the mod updates database
                updateCatalog = ModUpdaterHelper.DownloadModUpdateList();

                // 2. Find out what actually has been updated
                if (updateCatalog != null) {
                    availableUpdatesCatalog = ModUpdaterHelper.ListAvailableUpdates(updateCatalog);
                }
            });

            task.Start();
        }

        public override IEnumerator Leave(Oui next) {
            Everest.Loader.AutoLoadNewMods = true;

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
            availableUpdatesCatalog = new SortedDictionary<ModUpdateInfo, EverestModuleMetadata>();
            updatableMods = new List<ModUpdateHolder>();

            task = null;
        }

        public override void Update() {
            // check if the "press Back to restart" message has to be toggled
            if (menu != null && subHeader != null && (shouldRestart && menu.Focused) != willRestartMessageShown) {
                willRestartMessageShown = !willRestartMessageShown;
                if (willRestartMessageShown) {
                    subHeader.TextColor = Color.OrangeRed;
                    subHeader.Title = $"{Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")} ({Dialog.Clean("MODUPDATECHECKER_WILLRESTART")})";
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
                        // if there are multiple updates...
                        if (availableUpdatesCatalog.Count > 1) {
                            // display an "update all" button at the top of the list
                            updateAllButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_UPDATE_ALL"));
                            updateAllButton.Pressed(() => downloadAllMods());

                            menu.Add(updateAllButton);
                        }

                        // then, display one button per update
                        foreach (ModUpdateInfo update in availableUpdatesCatalog.Keys) {
                            EverestModuleMetadata metadata = availableUpdatesCatalog[update];

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

            if (Input.MenuCancel.Pressed) {
                // cancel any ongoing download (this has no effect if no download is ongoing anyway).
                ongoingUpdateCancelled = true;
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
                    Logger.Log("OuiModUpdateList", "Update was cancelled");

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
                Logger.Log("OuiModUpdateList", $"Updating {update.Name} failed");
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
            Logger.Log("OuiModUpdateList", $"Downloading {update.URL} to {zipPath}");

            Everest.Updater.DownloadFileWithProgress(update.URL, zipPath, (position, length, speed) => {
                if (length > 0) {
                    button.Label = $"{ModUpdaterHelper.FormatModName(update.Name)} ({((int) Math.Floor(100D * (position / (double) length)))}% @ {speed} KiB/s)";
                } else {
                    button.Label = $"{ModUpdaterHelper.FormatModName(update.Name)} ({((int) Math.Floor(position / 1000D))}KiB @ {speed} KiB/s)";
                }
                return !ongoingUpdateCancelled;
            });
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
                    if (doDownloadModUpdate(modupdate.update, modupdate.metadata, modupdate.button)) {
                        // if update is successful, remove this mod from the "update all" list
                        updatableMods.Remove(modupdate);
                        i--;
                    } else {
                        // stop trying to update further mods.
                        break;
                    }
                }

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

        private struct ModUpdateHolder {
            public ModUpdateInfo update;
            public EverestModuleMetadata metadata;
            public TextMenu.Button button;
        }
    }
}
