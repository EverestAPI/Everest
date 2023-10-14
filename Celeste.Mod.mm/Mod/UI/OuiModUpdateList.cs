using Celeste.Mod.Helpers;
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
        private TextMenuExt.SubHeaderExt subRestartHeader;
        private TextMenu.Button fetchingButton;
        private TextMenu.Button updateAllButton;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private Task task;

        private Task renderButtonsTask;

        private bool shouldRestart = false;

        private bool restartMenuAdded = false;

        private static List<ModUpdateHolder> updatableMods = null;

        private static bool ongoingUpdateCancelled = false;

        private static bool isFetchingDone => ModUpdaterHelper.IsAsyncUpdateCheckingDone();

        private bool menuOnScreen = false;

        public override IEnumerator Enter(Oui from) {
            Everest.Loader.AutoLoadNewMods = false;

            menu = new TextMenu();

            // display the title and a dummy "Fetching" button
            menu.Add(new TextMenu.Header(Dialog.Clean("MODUPDATECHECKER_MENU_TITLE")));

            menu.Add(subHeader = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")));
            
            if (updatableMods == null) {
                updatableMods = new List<ModUpdateHolder>();
                fetchingButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_FETCHING"));
                fetchingButton.Disabled = true;
                menu.Add(fetchingButton);
                task = new Task(generateAllButtons);
                task.Start();
            } else if (isFetchingDone) { // mods have been already fetched
                fetchingButton = null;
                // if there are multiple updates...
                if (updatableMods.Count > 1) {
                    // display an "update all" button at the top of the list
                    updateAllButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_UPDATE_ALL"));
                    updateAllButton.Pressed(() => downloadAllMods());
                    menu.Add(updateAllButton);
                }
                foreach (ModUpdateHolder modHolder in updatableMods) {
                    menu.Add(modHolder.GetButton(this)); // buttons should get automatically generated
                }
            } else { // fetching button without task
                fetchingButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_FETCHING"));
                fetchingButton.Disabled = true;
                menu.Add(fetchingButton);
            }

            Scene.Add(menu);

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

            if (updatableMods != null)
                foreach (ModUpdateHolder updMod in updatableMods) {
                    updMod.RemoveButton();
                }

            task = null;
            renderButtonsTask = null;
        }

        public override void Update() {
            if (menu == null || subHeader == null) { // not ready yet, skip for now
                base.Update();
                return;
            }

            if (renderButtonsTask != null) {
                renderButtonsTask.RunSynchronously();
                renderButtonsTask = null;
            }
            
            // check if the "press Back to restart" message has to be toggled
            if (menu.Focused && shouldRestart) {
                subHeader.TextColor = Color.OrangeRed;
                subHeader.Title = $"{Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")} ({Dialog.Clean("MODUPDATECHECKER_RESTARTNEEDED")})";
                addRestartButtons();
            } else if (!menu.Focused && ongoingUpdateCancelled && menuOnScreen) {
                subHeader.TextColor = Color.Gray;
                subHeader.Title = $"{Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")} ({Dialog.Clean("MODUPDATECHECKER_CANCELLING")})";
            } else {
                subHeader.TextColor = Color.Gray;
                subHeader.Title = Dialog.Clean("MODUPDATECHECKER_MENU_HEADER");
            }
            
            if (Input.MenuCancel.Pressed && !menu.Focused && menuOnScreen) { 
                // cancel any ongoing download (this has no effect if no download is ongoing anyway).
                ongoingUpdateCancelled = true;

                if (!isFetchingDone) {
                    // cancelling out during check for updates: go back to mod options instead
                    Audio.Play(SFX.ui_main_button_back);
                    Overworld.Goto<OuiModOptions>();

                    renderButtonsTask = null; // make sure no leftover tasks are there
                }
            } else if (Input.MenuCancel.Pressed && menu.Focused && Selected) {
                if (shouldRestart && subRestartHeader != null) {
                    Audio.Play(SFX.ui_main_button_invalid);
                } else {
                    // go back to mod options instead
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

        private void addRestartButtons() {
            if (restartMenuAdded) return;
            menu.Add(subRestartHeader = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODUPDATECHECKER_MENU_HEADER_RESTART")));
            TextMenu.Button shutdownButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_SHUTDOWN"));
            shutdownButton.Pressed(() => {
                new FadeWipe(base.Scene, false, delegate {
				    Engine.Scene = new Scene();
				    Engine.Instance.Exit();
			    });
            });
            TextMenu.Button restartButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_RESTART"));
            restartButton.Pressed(() => Everest.QuickFullRestart());
            menu.Add(restartButton); // Notice: order flipped
            menu.Add(shutdownButton); // I thought it was more natural as restart was default
            restartMenuAdded = true;
            if (updatableMods.Count == 0) { // nudge selection to the first possible if no mods are left
                menu.FirstSelection();
            }
        }

        private void generateAllButtons() {
            // 3. Render on screen
            Logger.Log(LogLevel.Verbose, "OuiModUpdateList", "Rendering updates");
            SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdates =
                ModUpdaterHelper.GetAsyncLoadedModUpdates();
            if (menu == null) return;
            
            if (availableUpdates == null) {
                // display an error message
                renderButtonsTask = new Task(() => {
                    menu.Remove(fetchingButton);
                    fetchingButton = null;
                    TextMenu.Button button = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_ERROR"));
                    button.Disabled = true;
                    menu.Add(button);
                });
                updatableMods = null;
                return;
            }
            
            if (availableUpdates.Count == 0) {
                // display a dummy "no update available" button
                renderButtonsTask = new Task(() => {
                    menu.Remove(fetchingButton);
                    fetchingButton = null;
                    TextMenu.Button button = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_NOUPDATE"));
                    button.Disabled = true;
                    menu.Add(button);
                });
                return;
            }

            List<TextMenu.Button> queuedItems = new List<TextMenu.Button>();

            // if there are multiple updates...
            if (availableUpdates.Count > 1) {
                // display an "update all" button at the top of the list
                updateAllButton = new TextMenu.Button(Dialog.Clean("MODUPDATECHECKER_UPDATE_ALL"));
                updateAllButton.Pressed(() => downloadAllMods());

                queuedItems.Add(updateAllButton);
            }

            // then, display one button per update
            foreach (ModUpdateInfo update in availableUpdates.Keys) {
                EverestModuleMetadata metadata = availableUpdates[update];

                string versionUpdate = metadata.VersionString;
                if (metadata.VersionString != update.Version)
                    versionUpdate = $"{metadata.VersionString} > {update.Version}";
                
                ModUpdateHolder holder = new ModUpdateHolder(update: update, metadata: metadata, buttonGenerator: _ => null);

                // NOTE TO FUTURE MAINTAINERS
                // This piece here has to be handled very carefully
                // The `buttonGenerator` may be called from different `OuiModUpdateList` instances and because of this
                // if we capture this we will get bad behaviors so BE CAREFUL! (by someone who totally did not spend to much time figuring this out)
                holder.buttonGenerator = instance => {
                    TextMenu.Button button = new TextMenu.Button(
                        $"{ModUpdaterHelper.FormatModName(metadata.Name)} " +
                        $"| v. {versionUpdate} " +
                        $"({new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(update.LastUpdate):yyyy-MM-dd})");
                    button.Pressed(() => {
                        // make the menu non-interactive
                        instance.menu.Focused = false;
                        button.Disabled = true;

                        // trigger the update download
                        downloadModUpdate(instance, holder);
                    });
                    // if there is more than one hash, it means there is multiple downloads for this mod. Thus, we can't update it manually.
                    // if there isn't, add it to the list of mods that can be updated via "update all"
                    if (update.xxHash.Count > 1) {
                        button.Disabled = true;
                    }

                    return button;
                };

                // if there is more than one hash, it means there is multiple downloads for this mod. Thus, we can't update it manually.
                // if there isn't, add it to the list of mods that can be updated via "update all"
                if (update.xxHash.Count <= 1) {
                    updatableMods.Add(holder);
                }

                queuedItems.Add(holder.GetButton(this));
                
            }
            renderButtonsTask = new Task(() => {
                foreach (TextMenu.Button button in queuedItems) {
                    menu.Remove(fetchingButton);
                    fetchingButton = null;
                    menu.Add(button);
                }
            });
        }

        /// <summary>
        /// Downloads and installs a mod update.
        /// </summary>
        /// <param name="instance">The current instance</param>
        /// <param name="modHolder">The relevant info for the mod</param>
        private static void downloadModUpdate(OuiModUpdateList instance, ModUpdateHolder modHolder) {
            instance.task = new Task(() => {
                try {
                    bool updateSuccess =
                        doDownloadModUpdate(instance, modHolder.update, modHolder.metadata, modHolder.GetButton(instance));
                    TextMenu currentMenu = instance.menu;
                    TextMenu.Button updateAllButton = instance.updateAllButton;

                    if (updateSuccess) {
                        // select another enabled option: the next one, or the last one if there is no next one.
                        if (currentMenu.Selection + 1 > currentMenu.LastPossibleSelection) {
                            currentMenu.Selection = currentMenu.LastPossibleSelection;
                        } else {
                            currentMenu.MoveSelection(1);
                        }

                        // remove this mod from the updatable mods list (it won't be updated by the "update all mods" button)
                        updatableMods.Remove(modHolder);
                        if (updatableMods.Count == 0 && updateAllButton != null) {
                            updateAllButton.Disabled = true;
                            updateAllButton.Label = Dialog.Clean("MODUPDATECHECKER_UPDATE_ALL_DONE");
                        }
                    } else {
                        // re-enable the button to allow the user to try again.
                        modHolder.GetButton(instance).Disabled = false;
                    }

                    // give the menu control back to the player
                    currentMenu.Focused = true;
                } catch (Exception ex) {
                    Logger.Log(LogLevel.Error, "OuiModUpdateList", "Starting mod update failed!" );
                    Logger.LogDetailed(ex);
                }
            });

            instance.task.Start();
        }

        /// <summary>
        /// Does the actual downloading of the mod. This is it's own function, to avoid duplicating the code
        /// </summary>
        /// <param name="instance">The instance to work on</param>
        /// <param name="update">The update info coming from the update server</param>
        /// <param name="mod">The mod metadata from Everest for the installed mod</param>
        /// <param name="button">The button for that mod shown on the interface</param>
        /// <returns>Bool whether the update failed or not</returns>
        private static bool doDownloadModUpdate(OuiModUpdateList instance, ModUpdateInfo update, EverestModuleMetadata mod, TextMenu.Button button) {
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
                instance.shouldRestart = true;

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
                    modupdate.GetButton(this).Disabled = true;
                }

                menu.Focused = false;
                for (int i = 0; i < updatableMods.Count; i++) {
                    ModUpdateHolder modupdate = updatableMods[i];

                    // focus on the mod being updated, to ensure it is on-screen.
                    focusOn(modupdate.GetButton(this));

                    if (doDownloadModUpdate(this, modupdate.update, modupdate.metadata, modupdate.GetButton(this))) {
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
                    modupdate.GetButton(this).Disabled = false;
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

        private class ModUpdateHolder {
            public ModUpdateInfo update;
            public EverestModuleMetadata metadata;
            public Func<OuiModUpdateList, TextMenu.Button> buttonGenerator;

            private TextMenu.Button _button;

            public ModUpdateHolder(ModUpdateInfo update, EverestModuleMetadata metadata, Func<OuiModUpdateList, TextMenu.Button> buttonGenerator) {
                this.update = update;
                this.metadata = metadata;
                this.buttonGenerator = buttonGenerator;
            }

            public TextMenu.Button GetButton(OuiModUpdateList instance) {
                if (_button == null)
                    _button = buttonGenerator(instance);
                return _button;
            }

            public void RemoveButton() {
                _button = null;
            }
        }
    }
}
