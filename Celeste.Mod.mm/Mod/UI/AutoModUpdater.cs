using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class AutoModUpdater : Scene {

        private HiresSnow snow;

        // animation management
        private MTexture cogwheel = null;
        private bool cogwheelSpinning = true;
        private float cogwheelStopTime;

        // state keeping
        private string modUpdatingMessage = null;
        private string modUpdatingSubMessage = null;
        private bool confirmToRestart = false;
        private bool confirmToContinue = false;
        private bool shouldContinue = false;

        private bool showCancel = false;
        private bool skipUpdate = false;

        public AutoModUpdater(HiresSnow snow) {
            this.snow = snow;

            cogwheel = GFX.Gui["reloader/cogwheel"];
        }

        public override void Begin() {
            base.Begin();

            Everest.Loader.AutoLoadNewMods = false;

            // add on-screen elements like GameLoader/OverworldLoader
            Add(new HudRenderer());
            Add(snow);
            ((patch_RendererList) RendererList).UpdateLists();

            // register the routine
            Entity entity = new Entity();
            entity.Add(new Coroutine(Routine()));
            Add(entity);
        }

        public override void End() {
            base.End();

            Everest.Loader.AutoLoadNewMods = true;
        }

        private IEnumerator Routine() {
            // display "checking for updates" message, in case the async task is not done yet.
            modUpdatingMessage = Dialog.Clean("AUTOUPDATECHECKER_CHECKING");

            // wait until the update check is over.
            showCancel = true;
            while (!ModUpdaterHelper.IsAsyncUpdateCheckingDone() && !skipUpdate) {
                yield return null;
            }
            showCancel = false;

            if (!skipUpdate) {
                SortedDictionary<ModUpdateInfo, EverestModuleMetadata> updateList = ModUpdaterHelper.GetAsyncLoadedModUpdates();
                if (updateList == null || updateList.Count == 0) {
                    // no mod update, clear message and continue right away.
                    modUpdatingMessage = null;
                    shouldContinue = true;
                } else {
                    // install mod updates
                    new Task(() => autoUpdate(updateList)).Start();
                }

                // wait until we can continue (async task finished, or player hit Confirm to continue)
                while (!shouldContinue)
                    yield return null;
            }

            // proceed to the title screen, as GameLoader would do it normally.
            Engine.Scene = new OverworldLoader(Overworld.StartMode.Titlescreen, snow);
        }


        private void autoUpdate(SortedDictionary<ModUpdateInfo, EverestModuleMetadata> updateList) {
            int currentlyUpdatedModIndex = 1;

            // this is common to all mods.
            string zipPath = Path.Combine(Everest.PathGame, "mod-update.zip");

            bool restartRequired = false;
            bool failuresOccured = false;

            // iterate through all mods to update now.
            foreach (ModUpdateInfo update in updateList.Keys) {
                // common beginning for all messages: f.e. [1/3] Auto-updating Polygon Dreams
                string progressString = $"[{currentlyUpdatedModIndex}/{updateList.Count}] {Dialog.Clean("AUTOUPDATECHECKER_UPDATING")} {ModUpdaterHelper.FormatModName(update.Name)}:";

                try {
                    // show the cancel button for downloading
                    showCancel = true;

                    // download it...
                    modUpdatingMessage = $"{progressString} {Dialog.Clean("AUTOUPDATECHECKER_DOWNLOADING")}";

                    Logger.Log(LogLevel.Verbose, "AutoModUpdater", $"Downloading {update.URL} to {zipPath}");
                    Func<int, long, int, bool> progressCallback = (position, length, speed) => {
                        if (skipUpdate) {
                            return false;
                        }

                        if (length > 0) {
                            modUpdatingMessage = $"{progressString} {Dialog.Clean("AUTOUPDATECHECKER_DOWNLOADING")} " +
                                $"({((int) Math.Floor(100D * (position / (double) length)))}% @ {speed} KiB/s)";
                        } else {
                            modUpdatingMessage = $"{progressString} {Dialog.Clean("AUTOUPDATECHECKER_DOWNLOADING")} " +
                                $"({((int) Math.Floor(position / 1000D))}KiB @ {speed} KiB/s)";
                        }
                        return true;
                    };

                    try {
                        Everest.Updater.DownloadFileWithProgress(update.URL, zipPath, progressCallback);
                    } catch (WebException e) {
                        Logger.Log(LogLevel.Warn, "AutoModUpdater", $"Download failed, trying mirror {update.MirrorURL}");
                        Logger.LogDetailed(e);
                        Everest.Updater.DownloadFileWithProgress(update.MirrorURL, zipPath, progressCallback);
                    }

                    // hide the cancel button for downloading, download is done
                    showCancel = false;

                    if (skipUpdate) {
                        Logger.Log(LogLevel.Verbose, "AutoModUpdater", "Update was skipped");

                        // try to delete mod-update.zip if it still exists.
                        ModUpdaterHelper.TryDelete(zipPath);

                        if (restartRequired) {
                            // stop trying to update mods; restart right away
                            break;
                        } else {
                            // proceed to the game right away
                            shouldContinue = true;
                            return;
                        }
                    }

                    // verify its checksum
                    modUpdatingMessage = $"{progressString} {Dialog.Clean("AUTOUPDATECHECKER_VERIFYING")}";
                    ModUpdaterHelper.VerifyChecksum(update, zipPath);

                    // install it
                    restartRequired = true;
                    modUpdatingMessage = $"{progressString} {Dialog.Clean("AUTOUPDATECHECKER_INSTALLING")}";
                    ModUpdaterHelper.InstallModUpdate(update, updateList[update], zipPath);

                } catch (Exception e) {
                    // update failed
                    Logger.Log(LogLevel.Warn, "AutoModUpdater", $"Updating {update.Name} failed");
                    Logger.LogDetailed(e);
                    failuresOccured = true;

                    // try to delete mod-update.zip if it still exists.
                    ModUpdaterHelper.TryDelete(zipPath);

                    // stop trying to update further mods.
                    break;
                }

                currentlyUpdatedModIndex++;
            }

            // don't show "cancel" anymore, install ended.
            showCancel = false;

            if (!failuresOccured) {
                // restart when everything is done
                modUpdatingMessage = Dialog.Clean("DEPENDENCYDOWNLOADER_RESTARTING");
                Thread.Sleep(1000);
                Everest.QuickFullRestart();
            } else {
                modUpdatingMessage = Dialog.Clean("AUTOUPDATECHECKER_FAILED");

                // stop the cogwheel
                cogwheelSpinning = false;
                cogwheelStopTime = RawTimeActive;

                if (restartRequired) {
                    // failures occured, restart is required
                    modUpdatingSubMessage = Dialog.Clean("AUTOUPDATECHECKER_REBOOT");
                    confirmToRestart = true;
                } else {
                    // failures occured, restart is not required
                    modUpdatingSubMessage = Dialog.Clean("AUTOUPDATECHECKER_CONTINUE");
                    confirmToContinue = true;
                }
            }
        }

        public override void Update() {
            base.Update();

            // check if Confirm is pressed to restart or proceed to the title screen.
            if (Input.MenuConfirm.Pressed) {
                if (confirmToRestart) {
                    Everest.QuickFullRestart();
                    confirmToRestart = false; // better safe than sorry
                }

                if (confirmToContinue)
                    shouldContinue = true;
            }

            // if Back is pressed, we should cancel the update.
            if (Input.MenuCancel.Pressed && showCancel) {
                skipUpdate = true;
                modUpdatingMessage = Dialog.Clean("AUTOUPDATECHECKER_SKIPPING");
            }
        }

        public override void Render() {
            base.Render();

            Draw.SpriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Engine.ScreenMatrix
            );

            Vector2 anchor = new Vector2(96f, 96f);

            // render the spinning cogwheel
            if (!(cogwheel?.Texture?.Texture?.IsDisposed ?? true)) {
                Vector2 cogPosition = anchor + new Vector2(0f, 0f);
                float cogScale = 0.25f;
                float cogRot = (cogwheelSpinning ? RawTimeActive : cogwheelStopTime) * 4f;

                // render a 2 pixel-thick cogwheel shadow / outline
                for (int x = -2; x <= 2; x++)
                    for (int y = -2; y <= 2; y++)
                        if (x != 0 || y != 0)
                            cogwheel.DrawCentered(cogPosition + new Vector2(x, y), Color.Black, cogScale, cogRot);

                // render the cogwheel itself
                cogwheel.DrawCentered(cogPosition, Color.White, cogScale, cogRot);
            }

            if (modUpdatingMessage != null) {
                // render sub-text (appears smaller under the text)
                drawText(modUpdatingMessage, anchor + new Vector2(48f, 0f), 0.8f);
            }

            if (modUpdatingSubMessage != null) {
                // render sub-text (appears smaller under the text)
                drawText(modUpdatingSubMessage, anchor + new Vector2(53f, 40f), 0.5f);
            }

            if (showCancel) {
                string label = Dialog.Clean("AUTOUPDATECHECKER_SKIP");
                ButtonUI.Render(new Vector2(1880f, 1024f), label, Input.MenuCancel, 0.5f, 1f);
            }

            Draw.SpriteBatch.End();
        }

        private void drawText(string text, Vector2 position, float scale) {
            Vector2 size = ActiveFont.Measure(text);
            ActiveFont.DrawOutline(text, position + new Vector2(size.X * 0.5f * scale, 0f), new Vector2(0.5f, 0.5f), Vector2.One * scale, Color.White, 2f, Color.Black);
        }
    }
}
