using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
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
    class OuiModUpdateList : Oui, OuiModOptions.ISubmenu {

        private TextMenu menu;
        private TextMenuExt.SubHeaderExt subHeader;
        private TextMenu.Button fetchingButton;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private Task task;

        private bool shouldRestart = false;

        private Dictionary<string, ModUpdateInfo> updateCatalog = null;
        private SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdatesCatalog = new SortedDictionary<ModUpdateInfo, EverestModuleMetadata>();

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

                            string versionUpdate = metadata.VersionString;
                            if (metadata.VersionString != update.Version)
                                versionUpdate = $"{metadata.VersionString} > {update.Version}";

                            TextMenu.Button button = new TextMenu.Button($"{metadata.Name.SpacedPascalCase()} | v. {versionUpdate} ({new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(update.LastUpdate):yyyy-MM-dd})");
                            button.Pressed(() => {
                                // make the menu non-interactive
                                menu.Focused = false;
                                button.Disabled = true;

                                // trigger the update download
                                downloadModUpdate(update, metadata, button);
                            });

                            // if there is more than one hash, it means there is multiple downloads for this mod. Thus, we can't update it manually.
                            if (update.xxHash.Count > 1)
                                button.Disabled = true;

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
                    button.Label = $"{update.Name.SpacedPascalCase()} ({Dialog.Clean("MODUPDATECHECKER_DOWNLOADING")})";
                    downloadMod(update, button, zipPath);

                    // verify its checksum
                    ModUpdaterHelper.VerifyChecksum(update, zipPath);

                    // mark restarting as required, as we will do weird stuff like closing zips afterwards.
                    if (!shouldRestart) {
                        shouldRestart = true;
                        subHeader.TextColor = Color.OrangeRed;
                        subHeader.Title = $"{Dialog.Clean("MODUPDATECHECKER_MENU_HEADER")} ({Dialog.Clean("MODUPDATECHECKER_WILLRESTART")})";
                    }

                    // install it
                    button.Label = $"{update.Name.SpacedPascalCase()} ({Dialog.Clean("MODUPDATECHECKER_INSTALLING")})";
                    ModUpdaterHelper.InstallModUpdate(update, mod, zipPath);

                    // done!
                    button.Label = $"{update.Name.SpacedPascalCase()} ({Dialog.Clean("MODUPDATECHECKER_UPDATED")})";

                    // select another enabled option: the next one, or the last one if there is no next one.
                    if (menu.Selection + 1 > menu.LastPossibleSelection)
                        menu.Selection = menu.LastPossibleSelection;
                    else
                        menu.MoveSelection(1);
                } catch (Exception e) {
                    // update failed
                    button.Label = $"{update.Name.SpacedPascalCase()} ({Dialog.Clean("MODUPDATECHECKER_FAILED")})";
                    Logger.Log("OuiModUpdateList", $"Updating {update.Name} failed");
                    Logger.LogDetailed(e);
                    button.Disabled = false;

                    // try to delete mod-update.zip if it still exists.
                    ModUpdaterHelper.TryDelete(zipPath);
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
                    button.Label = $"{update.Name.SpacedPascalCase()} ({((int) Math.Floor(100D * (position / (double) length)))}% @ {speed} KiB/s)";
                } else {
                    button.Label = $"{update.Name.SpacedPascalCase()} ({((int) Math.Floor(position / 1000D))}KiB @ {speed} KiB/s)";
                }
            });
        }
    }
}
