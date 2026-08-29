using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiMirrorTester : OuiGenericMenu, OuiModOptions.ISubmenu {
        public override string MenuName => Dialog.Clean("MODOPTIONS_MIRRORTESTING");
        private Task mirrorLoadingTask;
        private bool ongoingUpdateCancelled = false;
        public OuiMirrorTester() {
            backToParentMenu = onBackPressed;
        }

        protected override void addOptionsToMenu(patch_TextMenu menu) {
            TextMenuExt.SubHeaderExt loading = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MIRRORTESTING_EXPLANATION"));
            menu.Add(loading);

            mirrorLoadingTask = new Task(() => {
                int mirrorIndex = 0;
                List<string> mirrorNames = CoreModule.Settings.MirrorPreferences.Split(',').ToList();
                string downloadDestination = Path.Combine(Everest.PathTmp, $"mirror-tester.zip");
                Dictionary<string, long> speedForMirror = new();

                foreach (string mirrorUrl in ModUpdaterHelper.GetAllMirrorUrls("https://gamebanana.com/dl/1778093")) {
                    if (ongoingUpdateCancelled)
                        continue;
                    string mirrorName = Dialog.Clean("MODOPTIONS_COREMODULE_MIRRORPREFERENCES_" + mirrorNames[mirrorIndex]);
                    if (mirrorNames[mirrorIndex] == "gb") {
                        mirrorName = "GameBanana";
                    }
                    TextMenu.Button loadingMirror = new TextMenuExt.ButtonExt(Dialog.Clean("MODOPTIONS_MIRRORTESTING_DOWNLOADING").Replace("((mirror))", mirrorName)) { Disabled = true, TextColorDisabled = Color.LightGray };
                    menu.Add(loadingMirror);
                    Func<int, long, int, bool> progressCallback = (position, length, speed) => {
                        string speedString;
                        if (ongoingUpdateCancelled)
                            return false;
                        if (length > 0) {
                            speedString = $"{(int) Math.Floor(100D * (position / (double) length))}% @ {speed} KiB/s";
                        } else {
                            speedString = $"{(int) Math.Floor(position / 1000D)}KiB @ {speed} KiB/s";
                        }

                        loadingMirror.Label = Dialog.Clean("MODOPTIONS_MIRRORTESTING_DOWNLOADING").Replace("((mirror))", mirrorName) + $": {speedString}";
                        return true;
                    };


                    Logger.Verbose("OuiMirrorTester", $"Downloading from {mirrorUrl}");
                    bool finishedSuccessfully = false;
                    Stopwatch testerStopwatch = Stopwatch.StartNew();
                    try {
                        Everest.Updater.DownloadFileWithProgress(mirrorUrl, downloadDestination, progressCallback);
                        finishedSuccessfully = true;
                    } catch (TimeoutException) {
                    }
                    testerStopwatch.Stop();

                    if (finishedSuccessfully) {
                        loadingMirror.Label = Dialog.Clean("MODOPTIONS_MIRRORTESTING_DOWNLOADED").Replace("((mirror))", mirrorName) + $" {Math.Round((double) (testerStopwatch.ElapsedMilliseconds / 10)) / 100}s";
                        speedForMirror[mirrorNames[mirrorIndex]] = testerStopwatch.ElapsedMilliseconds;
                    } else {
                        loadingMirror.Label = Dialog.Clean("MODOPTIONS_MIRRORTESTING_TIMEDOUT").Replace("((mirror))", mirrorName);
                        speedForMirror[mirrorNames[mirrorIndex]] = long.MaxValue;
                    }
                    ModUpdaterHelper.TryDelete(downloadDestination);
                    mirrorIndex++;
                };
                if (!ongoingUpdateCancelled) {
                    mirrorNames.Sort((p1, p2) => speedForMirror[p1].CompareTo(speedForMirror[p2]));

                    List<string> mirrorPreferences = new List<string> {
                        "gb,jade,risingsunlight,otobot,wegfan",
                        "jade,risingsunlight,otobot,wegfan,gb",
                        "wegfan,otobot,jade,risingsunlight,gb",
                        "otobot,jade,risingsunlight,wegfan,gb",
                        "risingsunlight,jade,otobot,wegfan,gb"
                    };

                    CoreModule.Settings.MirrorPreferences = mirrorPreferences.Find(mirrors => mirrors.StartsWith(mirrorNames[0]));
                }
                Overworld.Goto<OuiModOptions>();
            });

            mirrorLoadingTask.Start();
        }

        private bool MessageDisplayed = false;
        public override void Update() {
            base.Update();

            if (mirrorLoadingTask != null && mirrorLoadingTask.IsCompleted && !mirrorLoadingTask.IsCompletedSuccessfully && !MessageDisplayed) {
                if (ongoingUpdateCancelled) {
                    menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MIRRORTESTING_CANCELED")));
                } else {
                    menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MIRRORTESTING_ERROR")));

                    throw mirrorLoadingTask.Exception;
                }
                MessageDisplayed = true;
            }
        }


        private void onBackPressed(Overworld overworld) {
            if (mirrorLoadingTask.IsCompleted) {
                overworld.Goto<OuiModOptions>();
            } else {
                ongoingUpdateCancelled = true;
            }
        }

        public override void Render() {
            base.Render();
        }

        public override IEnumerator Leave(Oui next) {
            IEnumerator orig = base.Leave(next);
            while (orig.MoveNext()) {
                yield return orig.Current;
            }

            // we left the screen: clean up all variables.
            mirrorLoadingTask = null;
            ongoingUpdateCancelled = false;
            MessageDisplayed = false;
        }
    }
}
