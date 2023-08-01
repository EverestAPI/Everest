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

        // list of currently favorite mods
        private HashSet<string> favoritedMods;
        // list of favorite mods when the menu was open
        private HashSet<string> favoritedModsOriginal;
        // dictionary mapping between the dependencies and the dependents
        private Dictionary<string, HashSet<string>> favoriteDependencies;

        private bool toggleDependencies = true;

        private bool protectFavorites = true;

        private TextMenuExt.SubHeaderExt restartMessage1;
        private TextMenuExt.SubHeaderExt restartMessage2;

        // maps each filename to its Everest modules
        private Dictionary<string, EverestModuleMetadata[]> modYamls;
        // maps each mod name to its newest Everest module
        private Dictionary<string, string> modFilename;

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
            Logger.Log(LogLevel.Verbose, "OuiModToggler", $"Found {allModYamls.Count} mod(s) with yaml files, took {stopwatch.ElapsedMilliseconds} ms");
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
                    modFilename = BuildModFilenameDictionary(modYamls);

                    // remove the "loading..." message
                    menu.Remove(loading);

                    // if there is a whitelist or temporary blacklist, warn the user that it will break those settings.
                    if (Everest.Loader.Whitelist != null || Everest.Loader.TemporaryBlacklist != null) {
                        menu.Add(restartMessage1 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_WHITELISTWARN")) { TextColor = Color.OrangeRed });
                    }

                    // display the warning about blacklist.txt + restarting
                    menu.Add(restartMessage1 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE_1")));
                    menu.Add(restartMessage2 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE_2")) { HeightExtra = 0f });
                    menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE_3")) { HeightExtra = 20f, TextColor = Color.Goldenrod });

                    // reduce spacing between the whitelist warning and the blacklist overwrite warning
                    if (Everest.Loader.Whitelist != null || Everest.Loader.TemporaryBlacklist != null) {
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
                            bool isFavoriteOrDependent = favoritedMods.Contains(toggle.Key) || favoriteDependencies.ContainsKey(toggle.Key);
                            if (protectFavorites && isFavoriteOrDependent) {
                                continue;
                            }

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

                    TextMenu.Item toggleProtectFavoritesButton;
                    menu.Add(toggleProtectFavoritesButton = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_MODTOGGLE_PROTECTFAVORITES"), protectFavorites)
                        .Change(value => protectFavorites = value));

                    TextMenuExt.EaseInSubMenuWithInputs favoriteToolTip = new TextMenuExt.EaseInSubMenuWithInputs(
                        string.Format(Dialog.Get("MODOPTIONS_MODTOGGLE_PROTECTFAVORITES_MESSAGE"), '|'),
                         '|',
                         new Monocle.VirtualButton[] { Input.MenuJournal },
                         false
                        ) { TextColor = Color.Gray };

                    menu.Add(favoriteToolTip);

                    toggleProtectFavoritesButton.OnEnter += () => {
                        // make the description appear.
                        favoriteToolTip.FadeVisible = true;
                    };
                    toggleProtectFavoritesButton.OnLeave += () => {
                        // make the description disappear.
                        favoriteToolTip.FadeVisible = false;
                    };


                    // "cancel" button to leave the screen without saving
                    menu.Add(new TextMenu.Button(Dialog.Clean("MODOPTIONS_MODTOGGLE_CANCEL")).Pressed(() => {
                        blacklistedMods = blacklistedModsOriginal;
                        favoritedMods = favoritedModsOriginal;
                        favoriteDependencies = null;
                        onBackPressed(Overworld);
                    }));

                    // reset the mods list
                    allMods = new List<string>();
                    blacklistedMods = new HashSet<string>();
                    favoritedMods = new HashSet<string>();
                    favoriteDependencies = new Dictionary<string, HashSet<string>>();

                    string[] files;
                    bool headerInserted;

                    // crawl directories
                    files = Directory.GetDirectories(Everest.Loader.PathMods);
                    Array.Sort(files, (a, b) => a.ToLowerInvariant().CompareTo(b.ToLowerInvariant()));
                    headerInserted = false;
                    for (int i = 0; i < files.Length; i++) {
                        string file = Path.GetFileName(files[i]);
                        if (file != "Cache") {
                            if (!headerInserted) {
                                menu.Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_DIRECTORIES")));
                                headerInserted = true;
                            }
                            addFileToMenu(menu, file);
                        }
                    }

                    // crawl zips
                    files = Directory.GetFiles(Everest.Loader.PathMods);
                    Array.Sort(files, (a, b) => a.ToLowerInvariant().CompareTo(b.ToLowerInvariant()));
                    headerInserted = false;
                    for (int i = 0; i < files.Length; i++) {
                        string file = Path.GetFileName(files[i]);
                        if (file.EndsWith(".zip")) {
                            if (!headerInserted) {
                                menu.Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_ZIPS")));
                                headerInserted = true;
                            }
                            addFileToMenu(menu, file);
                        }
                    }

                    // crawl map bins
                    files = Directory.GetFiles(Everest.Loader.PathMods);
                    Array.Sort(files, (a, b) => a.ToLowerInvariant().CompareTo(b.ToLowerInvariant()));
                    headerInserted = false;
                    for (int i = 0; i < files.Length; i++) {
                        string file = Path.GetFileName(files[i]);
                        if (file.EndsWith(".bin")) {
                            if (!headerInserted) {
                                menu.Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_BINS")));
                                headerInserted = true;
                            }
                            addFileToMenu(menu, file);
                        }
                    }

                    // sort the mods list alphabetically, for output in the blacklist.txt file later.
                    allMods.Sort((a, b) => a.ToLowerInvariant().CompareTo(b.ToLowerInvariant()));

                    // clone the list to be able to check if the list changed when leaving the menu.
                    blacklistedModsOriginal = new HashSet<string>(blacklistedMods);
                    favoritedModsOriginal = new HashSet<string>(favoritedMods);

                    // set colors to mods listings
                    updateHighlightedMods();

                    // snap the menu so that it doesn't show a scroll up.
                    menu.Y = menu.ScrollTargetY;


                    // loading is done!
                    modLoadingTask = null;
                });
            });
            modLoadingTask.Start();
        }

        private void addFileToMenu(TextMenu menu, string file) {
            TextMenu.OnOff option;

            bool enabled = !Everest.Loader.Blacklist.Contains(file);
            bool favorite = Everest.Loader.Favorites.Contains(file);
            menu.Add(option = (TextMenu.OnOff) new TextMenu.OnOff(file.Length > 40 ? file.Substring(0, 40) + "..." : file, enabled)
                .Change(b => {
                    if (b) {
                        removeFromBlacklist(file);
                    } else {
                        addToBlacklist(file);
                    }

                    updateHighlightedMods();
                }).AltPressed(() => {
                    if (favoritedMods.Contains(file)) {
                        removeFromFavorites(file);
                    } else {
                        addToFavorites(file);
                    }

                    updateHighlightedMods();
                }));

            allMods.Add(file);
            if (!enabled) {
                blacklistedMods.Add(file);
            }
            if (favorite) {
                // because we don't store the dependencies of favorite mods we want to call addToFavorites to walk the dependencies graph
                addToFavorites(file);
            }

            modToggles[file] = option;
        }

        private Dictionary<string, string> BuildModFilenameDictionary(Dictionary<string, EverestModuleMetadata[]> modYamls) {
            Dictionary<string, EverestModuleMetadata> everestModulesByModName = new();

            foreach (KeyValuePair<string, EverestModuleMetadata[]> pair in modYamls) {
                foreach (EverestModuleMetadata currentModule in pair.Value) {
                    if (everestModulesByModName.TryGetValue(currentModule.Name, out EverestModuleMetadata previousModule)) {
                        if (previousModule.Version < currentModule.Version) {
                            everestModulesByModName[currentModule.Name] = currentModule;
                        }
                    } else {
                        everestModulesByModName[currentModule.Name] = currentModule;
                    }
                }
            }


            return everestModulesByModName
                .ToDictionary(dictEntry => dictEntry.Key, dictEntry => Path.GetFileName(dictEntry.Value.PathArchive ?? dictEntry.Value.PathDirectory));
        }

        private void updateHighlightedMods() {
            // adjust the mods' color if they are required dependencies for other mods
            foreach (KeyValuePair<string, TextMenu.OnOff> toggle in modToggles) {
                Color unselectedColor = Color.White;
                if (favoritedMods.Contains(toggle.Key)) {
                    unselectedColor = Color.DeepPink;
                } else if (favoriteDependencies.ContainsKey(toggle.Key)) {
                    unselectedColor = Color.LightPink;
                } else if (modHasDependencies(toggle.Key)) {
                    unselectedColor = Color.Goldenrod;
                }
                ((patch_TextMenu.patch_Option<bool>) (object) toggle.Value).UnselectedColor = unselectedColor;
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

        public override void Update() {
            canGoBack = (modLoadingTask == null || modLoadingTask.IsCompleted || modLoadingTask.IsCanceled || modLoadingTask.IsFaulted);
            base.Update();
        }

        private void addToBlacklist(string file) {
            if (blacklistedMods.Contains(file)) {
                // already blacklisted
                return;
            }

            blacklistedMods.Add(file);
            Logger.Log(LogLevel.Verbose, "OuiModToggler", $"{file} was added to the blacklist");

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
            Logger.Log(LogLevel.Verbose, "OuiModToggler", $"{file} was removed from the blacklist");

            if (toggleDependencies && TryGetModDependenciesFileNames(file, out List<string> dependencies)) {
                // we should remove all of the mod's dependencies from the blacklist.
                foreach (string modFileName in dependencies) {
                    removeFromBlacklist(modFileName);
                    modToggles[modFileName].Index = 1;
                }
            }
        }

        private void addToFavorites(string modFileName) {
            favoritedMods.Add(modFileName);
            Logger.Log(LogLevel.Verbose, "OuiModToggler", $"{modFileName} was added to favorites");

            if (TryGetModDependenciesFileNames(modFileName, out List<string> dependenciesFileNames)) {
                foreach (string dependenciesFileName in dependenciesFileNames) {
                    addToFavoritesDependencies(dependenciesFileName, modFileName);
                }
            }
        }

        private void addToFavoritesDependencies(string modFileName, string dependentModFileName) {
            bool existsInFavoriteDependencies = favoriteDependencies.TryGetValue(modFileName, out HashSet<string> dependents);

            // If we have a cyclical dependencies we want to stop after the first occurrence of a mod, or if somehow a mod reached itself.
            if ((existsInFavoriteDependencies && dependents.Contains(dependentModFileName)) || modFileName == dependentModFileName) {
                return;
            }

            if (!existsInFavoriteDependencies) {
                dependents = favoriteDependencies[modFileName] = new HashSet<string>();
            }

            // Add dependent mod
            dependents.Add(dependentModFileName);
            Logger.Log(LogLevel.Verbose, "OuiModToggler", $"{modFileName} was added as a favorite dependency of {dependentModFileName}");


            // we want to walk the dependence graph and add all the sub-dependencies as dependencies of the original dependentModFileName  
            if (TryGetModDependenciesFileNames(modFileName, out List<string> dependenciesFileNames)) {
                foreach (string dependencyFileName in dependenciesFileNames) {
                    addToFavoritesDependencies(dependencyFileName, dependentModFileName);
                }
            }
        }

        private void removeFromFavorites(string modFileName) {
            favoritedMods.Remove(modFileName);
            Logger.Log(LogLevel.Verbose, "OuiModToggler", $"{modFileName} was removed from favorites");

            if (TryGetModDependenciesFileNames(modFileName, out List<string> dependenciesFileNames)) {
                foreach (string dependencyFileName in dependenciesFileNames) {
                    removeFromFavoritesDependencies(dependencyFileName, modFileName);
                }
            }
        }

        private void removeFromFavoritesDependencies(string modFileName, string dependentModFileName) {
            if (favoriteDependencies.TryGetValue(modFileName, out HashSet<string> dependents) && dependents.Contains(dependentModFileName)) {

                dependents.Remove(dependentModFileName);
                Logger.Log(LogLevel.Verbose, "OuiModToggler", $"{modFileName} was removed from being a favorite dependency of {dependentModFileName}");

                if (dependents.Count == 0) {
                    favoriteDependencies.Remove(modFileName);
                    Logger.Log(LogLevel.Verbose, "OuiModToggler", $"{modFileName} is no longer a favorite dependency");
                }

                if (TryGetModDependenciesFileNames(modFileName, out List<string> dependenciesFileNames)) {
                    foreach (string dependencyFileName in dependenciesFileNames) {
                        removeFromFavoritesDependencies(dependencyFileName, dependentModFileName);
                    }
                }
            }
        }

        private void onBackPressed(Overworld overworld) {
            // "back" only works if the loading is done.
            if (modLoadingTask == null || modLoadingTask.IsCompleted || modLoadingTask.IsCanceled || modLoadingTask.IsFaulted) {
                if (!favoritedModsOriginal.SetEquals(favoritedMods)) {
                    Everest.Loader.Favorites = favoritedMods;
                    using (StreamWriter writer = File.CreateText(Everest.Loader.PathFavorites)) {
                        // header
                        writer.WriteLine("# This is the favorite list. Lines starting with # are ignored.");
                        writer.WriteLine("");

                        foreach (string mod in favoritedMods) {
                            writer.WriteLine(mod);
                        }
                    }
                }
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

        private bool TryGetModDependenciesFileNames(string modFilename, out List<string> dependenciesFileNames) {
            if (modYamls.TryGetValue(modFilename, out EverestModuleMetadata[] metadatas)) {
                dependenciesFileNames = new List<string>();

                foreach (EverestModuleMetadata metadata in metadatas) {
                    foreach (string dependencyName in metadata.Dependencies.Select((dep) => dep.Name)) {
                        if (this.modFilename.TryGetValue(dependencyName, out string dependencyFileName)) {
                            dependenciesFileNames.Add(dependencyFileName);
                        }
                    }
                }

                return true;
            }


            dependenciesFileNames = null;
            return false;
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
            modFilename = null;
            modToggles = null;
            modLoadingTask = null;
            toggleDependencies = true;
            protectFavorites = false;
            favoritedMods = null;
            favoritedModsOriginal = null;
            favoriteDependencies = null;
        }
    }
}
