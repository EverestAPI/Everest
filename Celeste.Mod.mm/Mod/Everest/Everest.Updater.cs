using Celeste.Mod.UI;
using Ionic.Zip;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Celeste.Mod {
    public static partial class Everest {
        // TODO: General purpose updater for both Everest itself and any runtime mods.
        internal static class Updater {

            public class Entry {
                public readonly string Name;
                public readonly string Branch;
                public readonly string URL;
                public readonly int Build;
                public Entry(string name, string branch, string url, int version) {
                    Name = name;
                    Branch = branch ?? "";
                    URL = url;
                    Build = version;
                }
            }

            public class Source {

                public string NameDialog;

                public string Index;

                public Func<bool> IsCurrent;

                public Func<string, List<Entry>> ParseData;
#pragma warning disable CS0649
                public Func<string, Entry> ParseLine;
#pragma warning restore CS0649

                public virtual ReadOnlyCollection<Entry> Entries { get; protected set; }

                public string ErrorDialog { get; protected set; }

                private Task<Source> _RequestTask;
                public Task<Source> Request() {
                    if (_RequestTask != null)
                        return _RequestTask;
                    _RequestTask = new Task<Source>(() => _RequestStart());
                    _RequestTask.Start();
                    return _RequestTask;
                }
                private Source _RequestStart() {
                    Entries = null;
                    ErrorDialog = null;

                    string data;
                    try {
                        using (WebClient wc = new WebClient())
                            data = wc.DownloadString(Index);
                    } catch (Exception e) {
                        ErrorDialog = "updater_versions_err_download";
                        Logger.Log(LogLevel.Warn, "updater", "Failed requesting index: " + e.ToString());
                        return this;
                    }

                    List<Entry> entries = new List<Entry>();
                    if (ParseData != null) {
                        try {
                            entries.AddRange(ParseData(data));
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

                    // Highly convoluted scientific method to determine the entry order:
                    // - Order by first occurence of branch
                    // - Order by version inside branch
                    Dictionary<string, int> branchFirsts = new Dictionary<string, int>();
                    // Force stable, then master branches to appear first.
                    branchFirsts["stable"] = int.MaxValue;
                    branchFirsts["master"] = int.MaxValue - 2;

                    // Make sure that the branch we're on appears between stable and master.
                    // This ensures that people don't miss out on important stability updates,
                    // but don't get dragged onto the master branch by accident.
                    foreach (Entry entry in entries) {
                        if (entry.Build == Build) {
                            branchFirsts[entry.Branch] = int.MaxValue - 1;
                            break;
                        }
                    }

                    for (int i = 0; i < entries.Count; i++) {
                        Entry entry = entries[i];
                        if (!branchFirsts.ContainsKey(entry.Branch))
                            branchFirsts[entry.Branch] = i;
                    }

                    entries.Sort((a, b) => {
                        if (a.Branch != b.Branch)
                            return -(branchFirsts[a.Branch].CompareTo(branchFirsts[b.Branch]));
                        return -a.Build.CompareTo(b.Build);
                    });
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
                    NameDialog = "updater_src_buildbot",

                    Index = "https://dev.azure.com/EverestAPI/Everest/_apis/build/builds?api-version=5.0",

                    IsCurrent = () => VersionSuffix.StartsWith("azure-"),

                    ParseData = AzureDataParser("https://dev.azure.com/EverestAPI/Everest/_apis/build/builds/{0}/artifacts?artifactName=main&api-version=5.0&%24format=zip", 700)
                }
            };

            public static Task RequestAll() {
                if (Flags.IsDisabled || !Flags.SupportUpdatingEverest)
                    return new Task(() => { });

                Task[] tasks = new Task[Sources.Count];
                for (int i = 0; i < tasks.Length; i++) {
                    tasks[i] = Sources[i].Request();
                }
                return Task.Factory.ContinueWhenAll(tasks, finished => {
                    List<Entry> all = new List<Entry>();
                    foreach (Source source in Sources) {
                        if (source.Entries == null || !source.IsCurrent())
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

            private static Func<string, Entry> CommonLineParser(string root)
                => (line) => {
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
                    string branch = "master";

                    if (name.EndsWith(".zip"))
                        name = name.Substring(0, name.Length - 4);

                    if (name.StartsWith("build-"))
                        name = name.Substring(6);

                    int indexOfBranch = name.IndexOf('-');
                    if (indexOfBranch != -1) {
                        branch = name.Substring(indexOfBranch + 1);
                        name = name.Substring(0, indexOfBranch);
                    }

                    return new Entry(name, branch, url, int.Parse(Regex.Match(split[1], @"\d+").Value));
                };

            private static Func<string, List<Entry>> AzureDataParser(string artifactFormat, int offset)
                => (dataRaw) => {
                    List<Entry> entries = new List<Entry>();

                    JObject root = JObject.Parse(dataRaw);
                    JArray list = root["value"] as JArray;
                    foreach (JObject build in list) {
                        if (build["status"].ToObject<string>() != "completed" || build["result"].ToObject<string>() != "succeeded")
                            continue;

                        string reason = build["reason"].ToObject<string>();
                        if (reason != "manual" && reason != "individualCI")
                            continue;

                        int id = build["id"].ToObject<int>();
                        string branch = build["sourceBranch"].ToObject<string>().Replace("refs/heads/", "");
                        string url = string.Format(artifactFormat, id);
                        entries.Add(new Entry((id + offset).ToString(), branch, url, id + offset));
                    }

                    return entries;
                };

            public static Entry Newest { get; internal set; }
            public static bool HasUpdate => Newest != null && Newest.Build > Build;

            public static void Update(OuiLoggedProgress progress, Entry version = null) {
                if (!Flags.SupportUpdatingEverest) {
                    progress.Init<OuiModOptions>(Dialog.Clean("updater_title"), new Task(() => { }), 1).Progress = 1;
                    progress.LogLine("Updating not supported on this platform - cancelling.");
                    return;
                }

                if (version == null)
                    version = Newest;
                if (version == null) {
                    // Exit immediately.
                    progress.Init<OuiModOptions>(Dialog.Clean("updater_title"), new Task(() => { }), 1).Progress = 1;
                    progress.LogLine("No update - cancelling.");
                    return;
                }

                progress.Init<OuiHelper_Shutdown>(Dialog.Clean("updater_title"), new Task(() => _UpdateStart(progress, version)), 0);
            }
            private static void _UpdateStart(OuiLoggedProgress progress, Entry version) {
                // Last line printed on error.
                const string errorHint = "\nPlease create a new issue on GitHub @ https://github.com/EverestAPI/Everest\nor join the #modding_help channel on Discord (invite in the repo).\nMake sure to upload your log.txt";

                string zipPath = Path.Combine(PathGame, "everest-update.zip");
                string extractedPath = Path.Combine(PathGame, "everest-update");

                progress.LogLine($"Updating to {version.Name} (branch: {version.Branch}) @ {version.URL}");

                progress.LogLine($"Downloading");
                try {
                    DownloadFileWithProgress(version.URL, zipPath, (position, length, speed) => {
                        if (length > 0) {
                            progress.Lines[progress.Lines.Count - 1] =
                                $"Downloading: {((int) Math.Floor(100D * (position / (double) length)))}% @ {speed} KiB/s";
                            progress.Progress = position;
                        } else {
                            progress.Lines[progress.Lines.Count - 1] =
                                $"Downloading: {((int) Math.Floor(position / 1000D))}KiB @ {speed} KiB/s";
                        }

                        progress.ProgressMax = (int) length;
                    });
                } catch (Exception e) {
                    progress.LogLine("Download failed!");
                    e.LogDetailed();
                    progress.LogLine(errorHint);
                    progress.Progress = 0;
                    progress.ProgressMax = 1;
                    return;
                }
                progress.LogLine("Download finished.");

                progress.LogLine("Extracting update .zip");
                try {
                    if (extractedPath != PathGame && Directory.Exists(extractedPath))
                        Directory.Delete(extractedPath, true);

                    // Don't use zip.ExtractAll because we want to keep track of the progress.
                    using (ZipFile zip = new ZipFile(zipPath)) {
                        progress.LogLine($"{zip.Entries.Count} entries");
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
                    progress.LogLine("Extraction failed!");
                    e.LogDetailed();
                    progress.LogLine(errorHint);
                    progress.Progress = 0;
                    progress.ProgressMax = 1;
                    return;
                }
                progress.LogLine("Extraction finished.");

                progress.Progress = 1;
                progress.ProgressMax = 1;
                String action = "Restarting";
                if (Environment.OSVersion.Platform == PlatformID.Unix) {
                    action = "Updating";
                }
                progress.LogLine(action);
                for (int i = 3; i > 0; --i) {
                    progress.Lines[progress.Lines.Count - 1] = $"{action} in {i}";
                    Thread.Sleep(1000);
                }
                progress.Lines[progress.Lines.Count - 1] = action;

                // Start MiniInstaller in a separate process.
                try {
                    Process installer = new Process();
                    string installerPath = Path.Combine(extractedPath, "MiniInstaller.exe");
                    installer.StartInfo.FileName = installerPath;
                    if (Type.GetType("Mono.Runtime") != null) {
                        installer.StartInfo.FileName = "mono";
                        installer.StartInfo.Arguments = $"\"{installerPath}\"";
                        if (File.Exists("/bin/sh")) {
                            string pid = Process.GetCurrentProcess().Id.ToString();
                            installer.StartInfo.FileName = "/bin/sh";
                            installer.StartInfo.Arguments = $"-c \"kill -0 {pid}; while [ $? = \\\"0\\\" ]; do sleep 1; kill -0 {pid}; done; unset MONO_PATH LD_LIBRARY_PATH LC_ALL MONO_CONFIG; mono MiniInstaller.exe\"";
                        }
                    }
                    installer.StartInfo.WorkingDirectory = extractedPath;
                    if (Environment.OSVersion.Platform == PlatformID.Unix) {
                        installer.StartInfo.UseShellExecute = false;
                        installer.Start();
                        progress.LogLine("Patching the game in-place");
                        progress.LogLine("Restarting");
                    } else {
                        installer.Start();
                    }
                } catch (Exception e) {
                    progress.LogLine("Starting installer failed!");
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
            /// <param name="progressCallback">A method called periodically as the download progresses. Parameters are progress, length and speed in KiB/s</param>
            public static void DownloadFileWithProgress(string url, string destPath, Action<int, long, int> progressCallback) {
                DateTime timeStart = DateTime.Now;

                if (File.Exists(destPath))
                    File.Delete(destPath);

                // Manual buffered copy from web input to file output.
                // Allows us to measure speed and progress.
                using (WebClient wc = new WebClient())
                using (Stream input = wc.OpenRead(url))
                using (FileStream output = File.OpenWrite(destPath)) {
                    long length;
                    if (input.CanSeek) {
                        length = input.Length;
                    } else {
                        length = _ContentLength(url);
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

                        progressCallback(pos, length, speed);
                    }
                }
            }

            private static long _ContentLength(string url) {
                try {
                    HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
                    request.Method = "HEAD";
                    using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
                        return response.ContentLength;
                } catch (Exception) {
                    return 0;
                }
            }

        }
    }
}
