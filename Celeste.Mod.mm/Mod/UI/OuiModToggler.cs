using Ionic.Zip;
using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    class OuiModToggler : OuiGenericMenu, OuiModOptions.ISubmenu {
        public override string MenuName => Dialog.Clean("MODOPTIONS_MODTOGGLE");

        // list of all mods in the Mods folder
        private List<string> allMods;
        // list of currently blacklisted mods
        private HashSet<string> blacklistedMods;
        // list of blacklisted mods when the menu was open
        private HashSet<string> blacklistedModsOriginal;

        private bool toggleDependencies = true;

        private TextMenuExt.SubHeaderExt restartMessage1;
        private TextMenuExt.SubHeaderExt restartMessage2;

        private Dictionary<string, EverestModuleMetadata[]> modYamls;
        private Dictionary<string, TextMenu.OnOff> modToggles;
        private Task modLoadingTask;

        internal static Dictionary<string, EverestModuleMetadata[]> LoadAllModYamls(Action<float> progressCallback) {
            Stopwatch stopwatch = Stopwatch.StartNew();

            int processedFileCount = 0;
            int totalFileCount = Directory.GetFiles(Everest.Loader.PathMods).Length + Directory.GetDirectories(Everest.Loader.PathMods).Length;

            Dictionary<string, EverestModuleMetadata[]> allModYamls = new Dictionary<string, EverestModuleMetadata[]>();

            // we also are getting the loaded modules' metadata, to avoid reading them from disk again.
            IEnumerable<EverestModuleMetadata> alreadyLoadedModMetadata = Everest.Modules.Select(module => module.Metadata);

            // load zips
            string[] files = Directory.GetFiles(Everest.Loader.PathMods);
            for (int i = 0; i < files.Length; i++) {
                // update progress
                progressCallback((float) processedFileCount / totalFileCount);
                processedFileCount++;

                // check the file is a zip
                string file = Path.GetFileName(files[i]);
                if (!file.EndsWith(".zip"))
                    continue;

                // check if we didn't already load it, if not do it now
                EverestModuleMetadata[] metadatas = alreadyLoadedModMetadata.Where(meta => meta.PathArchive == files[i]).ToArray();
                if (metadatas.Length == 0) {
                    metadatas = loadZip(Path.Combine(Everest.Loader.PathMods, file));
                }

                // add it to the yaml list
                if (metadatas != null) {
                    allModYamls[file] = metadatas;
                }
            }

            // load directories
            files = Directory.GetDirectories(Everest.Loader.PathMods);
            for (int i = 0; i < files.Length; i++) {
                // update progress
                progressCallback((float) processedFileCount / totalFileCount);
                processedFileCount++;

                // ignore the Cache folder
                string file = Path.GetFileName(files[i]);
                if (file == "Cache")
                    continue;

                // check if we didn't already load it, if not do it now
                EverestModuleMetadata[] metadatas = alreadyLoadedModMetadata.Where(meta => meta.PathDirectory == files[i]).ToArray();
                if (metadatas.Length == 0) {
                    metadatas = loadDir(Path.Combine(Everest.Loader.PathMods, file));
                }

                // add it to the yaml list
                if (metadatas != null) {
                    allModYamls[file] = metadatas;
                }
            }

            // our list is complete!
            stopwatch.Stop();
            Logger.Log("OuiModToggler", $"Found {allModYamls.Count} mod(s) with yaml files, took {stopwatch.ElapsedMilliseconds} ms");
            return allModYamls;
        }

        private static EverestModuleMetadata[] loadZip(string archive) {
            try {
                using (ZipFile zip = new ZipFile(archive)) {
                    foreach (ZipEntry entry in zip.Entries) {
                        if (entry.FileName == "metadata.yaml") {
                            using (MemoryStream stream = entry.ExtractStream())
                            using (StreamReader reader = new StreamReader(stream)) {
                                EverestModuleMetadata meta = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata>(reader);
                                meta.PathArchive = archive;
                                meta.PostParse();
                                return new EverestModuleMetadata[] { meta };
                            }
                        }

                        if (entry.FileName == "multimetadata.yaml" ||
                            entry.FileName == "everest.yaml" ||
                            entry.FileName == "everest.yml") {
                            using (MemoryStream stream = entry.ExtractStream())
                            using (StreamReader reader = new StreamReader(stream)) {
                                if (!reader.EndOfStream) {
                                    EverestModuleMetadata[] multimetas = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata[]>(reader);
                                    foreach (EverestModuleMetadata multimeta in multimetas) {
                                        multimeta.PathArchive = archive;
                                        multimeta.PostParse();
                                    }
                                    return multimetas;
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "loader", $"Failed loading everest.yaml in archive {archive}: {e}");
            }

            return null;
        }

        private static EverestModuleMetadata[] loadDir(string dir) {
            try {
                string metaPath = Path.Combine(dir, "metadata.yaml");
                if (File.Exists(metaPath))
                    using (StreamReader reader = new StreamReader(metaPath)) {
                        EverestModuleMetadata meta = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata>(reader);
                        meta.PathDirectory = dir;
                        meta.PostParse();
                        return new EverestModuleMetadata[] { meta };
                    }

                metaPath = Path.Combine(dir, "multimetadata.yaml");
                if (!File.Exists(metaPath))
                    metaPath = Path.Combine(dir, "everest.yaml");
                if (!File.Exists(metaPath))
                    metaPath = Path.Combine(dir, "everest.yml");
                if (File.Exists(metaPath))
                    using (StreamReader reader = new StreamReader(metaPath)) {
                        if (!reader.EndOfStream) {
                            EverestModuleMetadata[] multimetas = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata[]>(reader);
                            foreach (EverestModuleMetadata multimeta in multimetas) {
                                multimeta.PathDirectory = dir;
                                multimeta.PostParse();
                            }
                            return multimetas;
                        }
                    }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "loader", $"Failed loading everest.yaml in directory {dir}: {e}");
            }

            return null;
        }

        public OuiModToggler() {
            backToParentMenu = onBackPressed;
        }

        protected override void addOptionsToMenu(TextMenu menu) {
            // for now, display a "loading" message.
            TextMenu.Button loading = new TextMenu.Button(Dialog.Clean("MODOPTIONS_MODTOGGLE_LOADING")) { Disabled = true };
            menu.Add(loading);

            modLoadingTask = new Task(() => {
                // load all the mod yamls (that can take some time), update the progress every 500ms so that the text doesn't go crazy since it is centered.
                Stopwatch updateTimer = Stopwatch.StartNew();
                modYamls = LoadAllModYamls(progress => {
                    if (updateTimer.ElapsedMilliseconds > 500) {
                        updateTimer.Restart();
                        loading.Label = $"{Dialog.Clean("MODOPTIONS_MODTOGGLE_LOADING")} ({(int) (progress * 100)}%)";
                    }
                });
                updateTimer.Stop();

                MainThreadHelper.Do(() => {
                    modToggles = new Dictionary<string, TextMenu.OnOff>();

                    // remove the "loading..." message
                    menu.Remove(loading);

                    // if there is a whitelist, warn the user that it will break those settings.
                    if (Everest.Loader.Whitelist != null) {
                        menu.Add(restartMessage1 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_WHITELISTWARN")) { TextColor = Color.OrangeRed });
                    }

                    // display the warning about blacklist.txt + restarting
                    menu.Add(restartMessage1 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE_1")));
                    menu.Add(restartMessage2 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE_2")) { HeightExtra = 0f });
                    menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE_3")) { HeightExtra = 20f, TextColor = Color.Goldenrod });

                    // reduce spacing between the whitelist warning and the blacklist overwrite warning
                    if (Everest.Loader.Whitelist != null) {
                        restartMessage1.HeightExtra = 30f;
                    }

                    // "enable all" and "disable all" buttons
                    menu.Add(new TextMenu.Button(Dialog.Clean("MODOPTIONS_MODTOGGLE_ENABLEALL")).Pressed(() => {
                        foreach (TextMenu.OnOff toggle in modToggles.Values) {
                            toggle.Index = 1;
                        }
                        blacklistedMods.Clear();
                        updateHighlightedMods();
                    }));
                    menu.Add(new TextMenu.Button(Dialog.Clean("MODOPTIONS_MODTOGGLE_DISABLEALL")).Pressed(() => {
                        blacklistedMods.Clear();
                        foreach (KeyValuePair<string, TextMenu.OnOff> toggle in modToggles) {
                            toggle.Value.Index = 0;
                            blacklistedMods.Add(toggle.Key);
                        }
                        updateHighlightedMods();
                    }));

                    // "toggle dependencies automatically" button
                    TextMenu.Item toggleDependenciesButton;
                    menu.Add(toggleDependenciesButton = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_MODTOGGLE_TOGGLEDEPS"), true)
                        .Change(value => toggleDependencies = value));

                    toggleDependenciesButton.AddDescription(menu, Dialog.Clean("MODOPTIONS_MODTOGGLE_TOGGLEDEPS_MESSAGE2"));
                    toggleDependenciesButton.AddDescription(menu, Dialog.Clean("MODOPTIONS_MODTOGGLE_TOGGLEDEPS_MESSAGE1"));

                    // "cancel" button to leave the screen without saving
                    menu.Add(new TextMenu.Button(Dialog.Clean("MODOPTIONS_MODTOGGLE_CANCEL")).Pressed(() => {
                        blacklistedMods = blacklistedModsOriginal;
                        onBackPressed(Overworld);
                    }));

                    // reset the mods list
                    allMods = new List<string>();
                    blacklistedMods = new HashSet<string>();

                    string[] files;
                    bool headerInserted;

                    // crawl directories
                    files = Directory.GetDirectories(Everest.Loader.PathMods);
                    headerInserted = false;
                    for (int i = 0; i < files.Length; i++) {
                        string file = Path.GetFileName(files[i]);
                        if (file != "Cache") {
                            if (!headerInserted) {
                                menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_DIRECTORIES")));
                                headerInserted = true;
                            }
                            addFileToMenu(menu, file);
                        }
                    }

                    // crawl zips
                    files = Directory.GetFiles(Everest.Loader.PathMods);
                    headerInserted = false;
                    for (int i = 0; i < files.Length; i++) {
                        string file = Path.GetFileName(files[i]);
                        if (file.EndsWith(".zip")) {
                            if (!headerInserted) {
                                menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_ZIPS")));
                                headerInserted = true;
                            }
                            addFileToMenu(menu, file);
                        }
                    }

                    // crawl map bins
                    files = Directory.GetFiles(Everest.Loader.PathMods);
                    headerInserted = false;
                    for (int i = 0; i < files.Length; i++) {
                        string file = Path.GetFileName(files[i]);
                        if (file.EndsWith(".bin")) {
                            if (!headerInserted) {
                                menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_BINS")));
                                headerInserted = true;
                            }
                            addFileToMenu(menu, file);
                        }
                    }

                    // sort the mods list alphabetically, for output in the blacklist.txt file later.
                    allMods.Sort();

                    // adjust the mods' color if they are required dependencies for other mods
                    foreach (KeyValuePair<string, TextMenu.OnOff> toggle in modToggles) {
                        if (modHasDependencies(toggle.Key)) {
                            ((patch_TextMenu.patch_Option<bool>) (object) toggle.Value).UnselectedColor = Color.Goldenrod;
                        }
                    }

                    // snap the menu so that it doesn't show a scroll up.
                    menu.Y = menu.ScrollTargetY;

                    // clone the list to be able to check if the list changed when leaving the menu.
                    blacklistedModsOriginal = new HashSet<string>(blacklistedMods);

                    // loading is done!
                    modLoadingTask = null;
                });
            });
            modLoadingTask.Start();
        }

        private void addFileToMenu(TextMenu menu, string file) {
            TextMenu.OnOff option;

            bool enabled = !Everest.Loader._Blacklist.Contains(file);
            menu.Add(option = (TextMenu.OnOff) new TextMenu.OnOff(file.Length > 40 ? file.Substring(0, 40) + "..." : file, enabled)
                .Change(b => {
                    if (b) {
                        removeFromBlacklist(file);
                    } else {
                        addToBlacklist(file);
                    }

                    updateHighlightedMods();
                }));

            allMods.Add(file);
            if (!enabled) {
                blacklistedMods.Add(file);
            }

            modToggles[file] = option;
        }

        private void updateHighlightedMods() {
            // adjust the mods' color if they are required dependencies for other mods
            foreach (KeyValuePair<string, TextMenu.OnOff> toggle in modToggles) {
                ((patch_TextMenu.patch_Option<bool>) (object) toggle.Value).UnselectedColor = modHasDependencies(toggle.Key) ? Color.Goldenrod : Color.White;
            }

            // turn the warning text about restarting/overwriting blacklist.txt orange/red if something was changed (so pressing Back will trigger a restart).
            if (blacklistedModsOriginal.SetEquals(blacklistedMods)) {
                restartMessage1.TextColor = Color.Gray;
                restartMessage2.TextColor = Color.Gray;
            } else {
                restartMessage1.TextColor = Color.OrangeRed;
                restartMessage2.TextColor = Color.OrangeRed;
            }
        }

        private void addToBlacklist(string file) {
            if (blacklistedMods.Contains(file)) {
                // already blacklisted
                return;
            }

            blacklistedMods.Add(file);
            Logger.Log("OuiModToggler", $"{file} was added to the blacklist");

            if (toggleDependencies && modYamls.TryGetValue(file, out EverestModuleMetadata[] metadatas)) {
                // we should blacklist all mods that has this mod as a dependency.
                foreach (EverestModuleMetadata metadata in metadatas) {
                    string modName = metadata.Name;

                    foreach (KeyValuePair<string, EverestModuleMetadata[]> otherMod in modYamls) {
                        if (!blacklistedMods.Contains(otherMod.Key) && otherMod.Value.Any(otherMeta => otherMeta.Dependencies.Any(dependency => dependency.Name == modName))) {
                            // this mod has a dependency on the current mod - turn it off too!
                            addToBlacklist(otherMod.Key);
                            modToggles[otherMod.Key].Index = 0;
                        }
                    }
                }
            }
        }

        private void removeFromBlacklist(string file) {
            if (!blacklistedMods.Contains(file)) {
                // already unblacklisted
                return;
            }

            blacklistedMods.Remove(file);
            Logger.Log("OuiModToggler", $"{file} was removed from the blacklist");

            if (toggleDependencies && modYamls.TryGetValue(file, out EverestModuleMetadata[] metadatas)) {
                // we should remove all of the mod's dependencies from the blacklist.
                foreach (EverestModuleMetadata metadata in metadatas) {
                    foreach (string dependency in metadata.Dependencies.Select(dep => dep.Name)) {
                        // we want to go through all the other mods' info to found the one we want.
                        KeyValuePair<string, EverestModuleMetadata>? found = null;
                        foreach (KeyValuePair<string, EverestModuleMetadata[]> candidateMetadatas in modYamls) {
                            foreach (EverestModuleMetadata candidateMetadata in candidateMetadatas.Value) {
                                if (candidateMetadata.Name == dependency) {
                                    // we found it!
                                    if (found == null || found.Value.Value.Version < candidateMetadata.Version) {
                                        found = new KeyValuePair<string, EverestModuleMetadata>(candidateMetadatas.Key, candidateMetadata);
                                    }
                                }
                            }
                        }
                        if (found.HasValue) {
                            // we found where the dependency is: activate it.
                            removeFromBlacklist(found.Value.Key);
                            modToggles[found.Value.Key].Index = 1;
                        }
                    }
                }
            }
        }

        private void onBackPressed(Overworld overworld) {
            // "back" only works if the loading is done.
            if (modLoadingTask == null || modLoadingTask.IsCompleted || modLoadingTask.IsCanceled || modLoadingTask.IsFaulted) {
                if (blacklistedModsOriginal.SetEquals(blacklistedMods)) {
                    // nothing changed, go back to Mod Options
                    overworld.Goto<OuiModOptions>();
                } else {
                    // save the blacklist
                    using (StreamWriter blacklistTxt = File.CreateText(Everest.Loader.PathBlacklist)) {
                        // header
                        blacklistTxt.WriteLine("# This is the blacklist. Lines starting with # are ignored.");
                        blacklistTxt.WriteLine("# File generated through the \"Toggle Mods\" menu in Mod Options");
                        blacklistTxt.WriteLine("");

                        // write all mods, commenting out the ones we want unblacklisted.
                        foreach (string mod in allMods) {
                            blacklistTxt.WriteLine(blacklistedMods.Contains(mod) ? mod : $"# {mod}");
                        }
                    }

                    // restart the game
                    Everest.QuickFullRestart();
                }
            }
        }

        private bool modHasDependencies(string modFilename) {
            if (modYamls.TryGetValue(modFilename, out EverestModuleMetadata[] metadatas)) {
                // this mod has a yaml, check all of the metadata entries (99% of the time there is one only).
                return metadatas.Any(metadata => {
                    string modName = metadata.Name;

                    // we want to check if a non-blacklisted mod has this mod as a dependency (by name).
                    return modYamls.Any(mod => !blacklistedMods.Contains(mod.Key) && modFilename != mod.Key
                        && mod.Value.Any(yaml => yaml.Dependencies.Any(dependency => dependency.Name == modName)));
                });

            }

            // this mod has no yaml, and as such, can't be a dependency of anything.
            return false;
        }

        public override IEnumerator Leave(Oui next) {
            IEnumerator orig = base.Leave(next);
            while (orig.MoveNext()) {
                yield return orig.Current;
            }

            // we left the screen: clean up all variables.
            allMods = null;
            blacklistedMods = null;
            blacklistedModsOriginal = null;
            restartMessage1 = null;
            restartMessage2 = null;
            modYamls = null;
            modToggles = null;
            modLoadingTask = null;
            toggleDependencies = true;
        }
    }
}
