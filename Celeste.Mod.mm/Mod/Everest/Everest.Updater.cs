﻿using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Celeste.Mod.UI;
using Ionic.Zip;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static partial class Everest {
        // TODO: General purpose updater for both Everest itself and any runtime mods.
        public static class Updater {

            public enum UpdatePriority {
                None, Low, High
            }

            public class Entry {
                public readonly string Name;
                public string Description;
                public readonly string URL;
                public readonly int Build;
                public readonly Source Source;
                public Entry(string name, string url, int version, Source source) {
                    Name = name;
                    URL = url;
                    Build = version;
                    Source = source;
                }
            }

            public class Source {

                public string Name;

                public string Description;

                public int MinimumBuild;

                public UpdatePriority UpdatePriority = UpdatePriority.Low;

                public Func<string> Index;

                public Func<Source, string, List<Entry>> ParseData;
#pragma warning disable CS0649
                public Func<string, Entry> ParseLine;
#pragma warning restore CS0649
                public virtual ReadOnlyCollection<Entry> Entries { get; protected set; }

                public string ErrorDialog { get; protected set; }

                private Task<Source> _RequestTask;
                public Task<Source> Request() {
                    if (_RequestTask != null)
                        return _RequestTask;
                    _RequestTask = new Task<Source>(() => {
                        try {
                            return _RequestStart();
                        } catch (Exception e) {
                            ErrorDialog = "updater_versions_err_download";
                            Logger.Log(LogLevel.Warn, "updater", "Uncaught exception while loading version list");
                            Logger.LogDetailed(e);
                            return this;
                        }
                    });
                    _RequestTask.Start();
                    return _RequestTask;
                }
                private Source _RequestStart() {
                    Entries = null;
                    ErrorDialog = null;

                    string data;
                    try {
                        Logger.Log(LogLevel.Debug, "updater", "Attempting to download update list from source: " + Index);
                        using (HttpClient hc = new CompressedHttpClient())
                            data = hc.GetStringAsync(Index()).Result;
                    } catch (Exception e) {
                        ErrorDialog = "updater_versions_err_download";
                        Logger.Log(LogLevel.Warn, "updater", "Failed requesting index: " + e.ToString());
                        return this;
                    }

                    List<Entry> entries = new List<Entry>();
                    if (ParseData != null) {
                        try {
                            entries.AddRange(ParseData(this, data));
                        } catch (Exception e) {
                            ErrorDialog = "updater_versions_err_format";
                            Logger.Log(LogLevel.Warn, "updater", "Failed parsing index: " + e.ToString());
                            return this;
                        }
                    } else {
                        string[] lines = data.Split('\n');
                        for (int i = 0; i < lines.Length; i++) {
                            string line = lines[i].Trim('\r', '\n').Trim();
                            if (line.Length == 0 || line.StartsWith("#"))
                                continue;

                            try {
                                Entry entry = ParseLine(line);
                                if (entry != null)
                                    entries.Add(entry);
                            } catch (Exception e) {
                                ErrorDialog = "updater_versions_err_format";
                                Logger.Log(LogLevel.Warn, "updater", "Failed parsing index: " + e.ToString());
                                return this;
                            }
                        }
                    }

                    for (int i = 0; i < entries.Count; i++) {
                        if (entries[i].Build < MinimumBuild)
                            entries.RemoveAt(i--);
                    }

                    Entries = new ReadOnlyCollection<Entry>(entries);
                    return this;
                }

                public void Clear() {
                    if (_RequestTask != null && !_RequestTask.IsCompleted && !_RequestTask.IsCanceled && !_RequestTask.IsFaulted)
                        // We don't cancel any running tasks.
                        return;

                    _RequestTask = null;
                    Entries = null;
                    ErrorDialog = null;
                }

            }

            public static List<Source> Sources = new List<Source>() {
                new Source {
                    Name = "updater_src_stable",
                    Description = "updater_src_release_github",
                    MinimumBuild = 3960,

                    UpdatePriority = UpdatePriority.High,

                    Index = GetEverestUpdaterDatabaseURL,
                    ParseData = UpdateListParser("stable")
                },
                new Source {
                    Name = "updater_src_beta",
                    Description = "updater_src_release_github",
                    MinimumBuild = 3960,

                    Index = GetEverestUpdaterDatabaseURL,
                    ParseData = UpdateListParser("beta")
                },
                new Source {
                    Name = "updater_src_dev",
                    Description = "updater_src_buildbot_azure",
                    MinimumBuild = 3960,

                    Index = GetEverestUpdaterDatabaseURL,
                    ParseData = UpdateListParser("dev")
                },
                new Source {
                    Name = "updater_src_core",
                    Description = "updater_src_buildbot_azure",

                    Index = GetEverestUpdaterDatabaseURL,
                    ParseData = UpdateListParser("core")
                },
            };

            public static Task RequestAll() {
                if (!Flags.SupportUpdatingEverest)
                    return new Task(() => { });

                Task[] tasks = new Task[Sources.Count];
                for (int i = 0; i < tasks.Length; i++) {
                    tasks[i] = Sources[i].Request();
                }
                return Task.Factory.ContinueWhenAll(tasks, finished => {
                    List<Entry> all = new List<Entry>();
                    foreach (Source source in Sources) {
                        if (source.Entries == null || source.Name != CoreModule.Settings.CurrentBranch)
                            continue;
                        all.AddRange(source.Entries);
                    }

                    if (all.Count == 0)
                        return;

                    // look up the installed version in the table (or the latest one by default).
                    int currentBuildIndex = all.FindIndex(entry => entry.Build == Build);
                    if (currentBuildIndex == -1)
                        currentBuildIndex = 0;

                    // find the latest version (highest build number), taking only the elements that are higher in the list into account.
                    Newest = all[0];
                    for (int i = 1; i <= currentBuildIndex; i++) {
                        if (all[i].Build > Newest.Build) {
                            Newest = all[i];
                        }
                    }
                });
            }

            private static string _everestUpdaterDatabaseURL;
            private static string GetEverestUpdaterDatabaseURL() {
                if (string.IsNullOrEmpty(_everestUpdaterDatabaseURL)) {
                    using (HttpClient hc = new CompressedHttpClient()) {
                        Logger.Log(LogLevel.Verbose, "updater", "Fetching everest updater database URL");

                        UriBuilder uri = new UriBuilder(hc.GetStringAsync("https://everestapi.github.io/everestupdater.txt").Result.Trim());
                        if ((uri.Query?.Length ?? 0) > 1)
                            uri.Query = uri.Query.Substring(1) + "&supportsNativeBuilds=true";
                        else
                            uri.Query = "supportsNativeBuilds=true";
                        _everestUpdaterDatabaseURL = uri.ToString();
                    }
                }
                return _everestUpdaterDatabaseURL;
            }

            private static Func<Source, string, Entry> CommonLineParser(string root)
                => (source, line) => {
                    string[] split = line.Split(' ');
                    if (split.Length < 2 || split.Length > 3)
                        throw new Exception("Version list format incompatible!");

                    string url = split[0];
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                        // The index contains a relative path.
                        url = root + url;

                    if (!url.EndsWith("/" + split[1]))
                        throw new Exception("URL (first column) must end in filename (second column)!");

                    string name = split[1];
                    string branch = "dev";

                    if (name.EndsWith(".zip"))
                        name = name.Substring(0, name.Length - 4);

                    if (name.StartsWith("build-"))
                        name = name.Substring(6);

                    int indexOfBranch = name.IndexOf('-');
                    if (indexOfBranch != -1) {
                        branch = name.Substring(indexOfBranch + 1);
                        name = name.Substring(0, indexOfBranch);
                    }

                    return new Entry(name, url, int.Parse(Regex.Match(split[1], @"\d+").Value), source);
                };

            public static Func<Source, string, List<Entry>> UpdateListParser(string branch)
                => (source, dataRaw) => {
                    List<Entry> entries = new List<Entry>();

                    JArray list = JArray.Parse(dataRaw);
                    foreach (JObject release in list) {
                        if (release["branch"].ToString() == branch) {
                            int build = release["version"].ToObject<int>();
                            string url = release["mainDownload"].ToString();
                            entries.Add(new Entry(build.ToString(), url, build, source));
                        }
                    }
                    return entries;
                };

            public static Func<Source, string, List<Entry>> AzureBuildsParser(string artifactFormat, int offset)
                => (source, dataRaw) => {
                    List<Entry> entries = new List<Entry>();

                    JObject root = JObject.Parse(dataRaw);
                    JArray list = root["value"] as JArray;
                    foreach (JObject build in list) {
                        int id = build["id"].ToObject<int>();
                        string url = string.Format(artifactFormat, id);
                        Entry entry = new Entry((id + offset).ToString(), url, id + offset, source);
                        try { entry.Description = build.SelectToken("triggerInfo['ci.message']").ToString().Split('\n')[0]; } catch { }
                        entries.Add(entry);
                    }

                    return entries;
                };

            public static Func<Source, string, List<Entry>> GitHubReleasesParser(int offset, bool prerelease = false)
                => (source, dataRaw) => {
                    List<Entry> entries = new List<Entry>();

                    JArray list = JArray.Parse(dataRaw);
                    foreach (JObject release in list) {
                        if (prerelease != release["prerelease"].ToObject<bool>())
                            continue;

                        string build = Regex.Match(release["name"].ToString(), @"\d+$").Value;
                        string url = null;
                        foreach (JObject asset in release["assets"] as JArray) {
                            if (asset["name"].ToString() == "main.zip") {
                                url = asset["browser_download_url"].ToString();
                                break;
                            }
                        }
                        if (url is null)
                            throw new Exception("main.zip asset not found for release");

                        entries.Add(new Entry(build, url, int.Parse(build), source));
                    }

                    return entries;
                };

            public static Entry Newest { get; internal set; }
            public static bool HasUpdate => Newest != null && Newest.Build > Build;
            public static bool UpdateFailed { get; internal set; }

            internal static void CheckForUpdateFailure() {
                string updateBuildPath = Path.Combine(PathGame, "everest-update", "update-build.txt");
                if (!File.Exists(updateBuildPath))
                    return;

                try {
                    if (Build != int.Parse(File.ReadAllText(updateBuildPath)))
                        UpdateFailed = true;
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "updater", "Exception when trying to determine update build number");
                    Logger.LogDetailed(e);
                    UpdateFailed = true;
                } finally {
                    File.Delete(updateBuildPath);
                }
            }

            public static void Update(OuiLoggedProgress progress, Entry version = null) {
                if (!Flags.SupportUpdatingEverest) {
                    progress.Init<OuiModOptions>(Dialog.Clean("updater_title"), new Task(() => { }), 1).Progress = 1;
                    progress.LogLine(Dialog.Clean("EVERESTUPDATER_NOTSUPPORTED"));
                    return;
                }

                if (version == null)
                    version = Newest;
                if (version == null) {
                    // Exit immediately.
                    progress.Init<OuiModOptions>(Dialog.Clean("updater_title"), new Task(() => { }), 1).Progress = 1;
                    progress.LogLine(Dialog.Clean("EVERESTUPDATER_NOUPDATE"));
                    return;
                }

                // The user has made their choice, so we will save the desired branch now.
                CoreModule.Settings.CurrentBranch = version.Source.Name;
                CoreModule.Instance.SaveSettings();
                progress.Init<OuiHelper_Shutdown>(Dialog.Clean("updater_title"), new Task(() => _UpdateStart(progress, version)), 0);
            }
            private static void _UpdateStart(OuiLoggedProgress progress, Entry version) {
                // Last line printed on error.
                string errorHint = $"\n{Dialog.Clean("EVERESTUPDATER_ERRORHINT1")}\n{Dialog.Clean("EVERESTUPDATER_ERRORHINT2")}\n{Dialog.Clean("EVERESTUPDATER_ERRORHINT3")}";

                string zipPath = Path.Combine(PathGame, "everest-update.zip");
                string extractedPath = Path.Combine(PathGame, "everest-update");

                progress.LogLine(string.Format(Dialog.Get("EVERESTUPDATER_UPDATING"), version.Name, version.Source.Name.DialogClean(), version.URL));

                progress.LogLine(Dialog.Clean("EVERESTUPDATER_DOWNLOADING"));
                try {
                    DownloadFileWithProgress(version.URL, zipPath, (position, length, speed) => {
                        if (length > 0) {
                            progress.Lines[progress.Lines.Count - 1] =
                                $"{Dialog.Clean("EVERESTUPDATER_DOWNLOADING_PROGRESS")} {((int) Math.Floor(100D * (position / (double) length)))}% @ {speed} KiB/s";
                            progress.Progress = position;
                        } else {
                            progress.Lines[progress.Lines.Count - 1] =
                                $"{Dialog.Clean("EVERESTUPDATER_DOWNLOADING_PROGRESS")} {((int) Math.Floor(position / 1000D))}KiB @ {speed} KiB/s";
                        }

                        progress.ProgressMax = (int) length;
                        return true; // continue downloading
                    });
                } catch (Exception e) {
                    progress.LogLine(Dialog.Clean("EVERESTUPDATER_DOWNLOADFAILED"));
                    e.LogDetailed();
                    progress.LogLine(errorHint);
                    progress.Progress = 0;
                    progress.ProgressMax = 1;
                    return;
                }
                progress.LogLine(Dialog.Clean("EVERESTUPDATER_DOWNLOADFINISHED"));

                progress.LogLine(Dialog.Clean("EVERESTUPDATER_EXTRACTING"));

                bool isNative = true;
                try {
                    if (extractedPath != PathGame && Directory.Exists(extractedPath))
                        Directory.Delete(extractedPath, true);

                    // Don't use zip.ExtractAll because we want to keep track of the progress.
                    using (ZipFile zip = new ZipFile(zipPath)) {
                        progress.LogLine($"{zip.Entries.Count} {Dialog.Clean("EVERESTUPDATER_ZIPENTRIES")}");
                        progress.Progress = 0;
                        progress.ProgressMax = zip.Entries.Count;

                        foreach (ZipEntry entry in zip.Entries) {
                            if (entry.FileName.Replace('\\', '/').EndsWith("/")) {
                                progress.Progress++;
                                continue;
                            }

                            string entryName = entry.FileName;
                            if (entryName.StartsWith("main/"))
                                entryName = entryName.Substring(5);

                            if (entryName == "MiniInstaller.exe")
                                isNative = false;

                            string fullPath = Path.Combine(extractedPath, entryName);
                            string fullDir = Path.GetDirectoryName(fullPath);
                            if (!Directory.Exists(fullDir))
                                Directory.CreateDirectory(fullDir);
                            if (File.Exists(fullPath))
                                File.Delete(fullPath);
                            progress.LogLine($"{entry.FileName} -> {fullPath}");
                            using (Stream stream = File.OpenWrite(fullPath))
                                entry.Extract(stream);
                            progress.Progress++;
                        }
                    }
                } catch (Exception e) {
                    progress.LogLine(Dialog.Clean("EVERESTUPDATER_EXTRACTIONFAILED"));
                    e.LogDetailed();
                    progress.LogLine(errorHint);
                    progress.Progress = 0;
                    progress.ProgressMax = 1;
                    return;
                }
                progress.LogLine(Dialog.Clean("EVERESTUPDATER_EXTRACTIONFINISHED"));

                progress.Progress = 1;
                progress.ProgressMax = 1;
                string action = Dialog.Clean("EVERESTUPDATER_RESTARTING");
                progress.LogLine(action);
                for (int i = 3; i > 0; --i) {
                    progress.Lines[progress.Lines.Count - 1] = string.Format(Dialog.Get("EVERESTUPDATER_RESTARTINGIN"), i);
                    Thread.Sleep(1000);
                }
                progress.Lines[progress.Lines.Count - 1] = action;

                try {
                    // Store the update version for later
                    File.WriteAllText(Path.Combine(extractedPath, "update-build.txt"), version.Build.ToString());

                    // Start MiniInstaller in a separate process.
                    Process installer = new Process();

                    string installerPath;
                    if (!isNative) {
                        installer.StartInfo.FileName = installerPath = Path.Combine(extractedPath, "MiniInstaller.exe");
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                            // Start MiniInstaller using mono
                            installer.StartInfo.FileName = "mono";
                            installer.StartInfo.Arguments = $"\"{installerPath}\"";
                            if (File.Exists("/bin/sh")) {
                                string pid = Process.GetCurrentProcess().Id.ToString();
                                installer.StartInfo.FileName = "/bin/sh";
                                string pathToMono = "mono";
                                if (File.Exists("/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono")) {
                                    pathToMono = "/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono";
                                }
                                installer.StartInfo.Arguments = $"-c \"kill -0 {pid}; while [ $? = \\\"0\\\" ]; do sleep 1; kill -0 {pid}; done; unset MONO_PATH LD_LIBRARY_PATH LC_ALL MONO_CONFIG; {pathToMono} MiniInstaller.exe\"";
                            }
                        }
                    } else {
                        installer.StartInfo.FileName = installerPath = Path.Combine(extractedPath,
                            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                                (RuntimeInformation.OSArchitecture == Architecture.X64 ? "MiniInstaller-win64.exe" : "MiniInstaller-win.exe") :
                            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "MiniInstaller-linux" :
                            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "MiniInstaller-osx" :
                            throw new Exception("Unknown OS platform")
                        );
                        installer.StartInfo.Environment["EVEREST_UPDATE_CELESTE_PID"] = Process.GetCurrentProcess().Id.ToString();
                    }

                    if (!File.Exists(installerPath))
                        throw new Exception("Couldn't find MiniInstaller executable");

                    if (isNative && (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))) {
                        // Make MiniInstaller executable
                        Process chmodProc = Process.Start(new ProcessStartInfo("chmod", $"u+x \"{installer.StartInfo.FileName}\""));
                        chmodProc.WaitForExit();
                        if (chmodProc.ExitCode != 0)
                            throw new Exception("Failed to set MiniInstaller executable flag");
                    }

                    installer.StartInfo.WorkingDirectory = extractedPath;
                    installer.StartInfo.UseShellExecute = false;
                    installer.Start();
                } catch (Exception e) {
                    progress.LogLine(Dialog.Clean("EVERESTUPDATER_STARTINGFAILED"));
                    e.LogDetailed();
                    progress.LogLine(errorHint);
                    progress.Progress = 0;
                    progress.ProgressMax = 1;
                }
            }

            /// <summary>
            /// Downloads a file and calls the progressCallback parameter periodically with progress information.
            /// This can be used to display the download progress on screen.
            /// </summary>
            /// <param name="url">The URL to download the file from</param>
            /// <param name="destPath">The path the file should be downloaded to</param>
            /// <param name="progressCallback">A method called periodically as the download progresses. Parameters are progress, length and speed in KiB/s.
            /// Should return true for the download to continue, false for it to be cancelled.</param>
            public static void DownloadFileWithProgress(string url, string destPath, Func<int, long, int, bool> progressCallback) {
                DateTime timeStart = DateTime.Now;

                if (File.Exists(destPath))
                    File.Delete(destPath);

                using (HttpClient client = new CompressedHttpClient()) {
                    client.Timeout = TimeSpan.FromMilliseconds(10000);
                    client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");

                    // Manual buffered copy from web input to file output.
                    // Allows us to measure speed and progress.
                    using (HttpResponseMessage response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                    using (Stream input = response.Content.ReadAsStream())
                    using (FileStream output = File.OpenWrite(destPath)) {
                        if (input.CanTimeout)
                            input.ReadTimeout = 10000;

                        long length;
                        if (input.CanSeek) {
                            length = input.Length;
                        } else {
                            length = response.Content.Headers.ContentLength ?? 0;
                        }

                        progressCallback(0, length, 0);

                        byte[] buffer = new byte[4096];
                        DateTime timeLastSpeed = timeStart;
                        int read = 1;
                        int readForSpeed = 0;
                        int pos = 0;
                        int speed = 0;
                        int count = 0;
                        TimeSpan td;
                        while (read > 0) {
                            count = length > 0 ? (int) Math.Min(buffer.Length, length - pos) : buffer.Length;
                            read = input.Read(buffer, 0, count);
                            output.Write(buffer, 0, read);
                            pos += read;
                            readForSpeed += read;

                            td = DateTime.Now - timeLastSpeed;
                            if (td.TotalMilliseconds > 100) {
                                speed = (int) ((readForSpeed / 1024D) / td.TotalSeconds);
                                readForSpeed = 0;
                                timeLastSpeed = DateTime.Now;
                            }

                            if (!progressCallback(pos, length, speed)) {
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
