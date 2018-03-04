using Celeste.Mod.UI;
using Ionic.Zip;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Helpers;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static partial class Everest {
        // TODO: General purpose updater for both Everest itself and any runtime mods.
        internal static class Updater {

            public class Entry {
                public readonly string Name;
                public readonly string Branch;
                public readonly string URL;
                public readonly Version Version;
                public Entry(string name, string branch, string url, Version version) {
                    Name = name;
                    Branch = branch ?? "";
                    URL = url;
                    Version = version;
                }
            }

            public class Source {

                public string NameDialog;

                public string Index;

                public Func<bool> IsCurrent;

                public Func<string, Entry> ParseLine;

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
                            data  = wc.DownloadString(Index);
                    } catch (Exception e) {
                        ErrorDialog = "updater_versions_err_download";
                        Logger.Log(LogLevel.Warn, "updater", "Failed requesting index: " + e.ToString());
                        return this;
                    }

                    List<Entry> entries = new List<Entry>();
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

                    // Highly convoluted scientific method to determine the entry order:
                    // - Order by first occurence of branch
                    // - Order by version inside branch
                    Dictionary<string, int> branchFirsts = new Dictionary<string, int>();
                    // Force master branch to appear first.
                    branchFirsts["master"] = int.MaxValue;
                    for (int i = 0; i < entries.Count; i++) {
                        Entry entry = entries[i];
                        if (!branchFirsts.ContainsKey(entry.Branch))
                            branchFirsts[entry.Branch] = i;
                    }
                    entries.Sort((a, b) => {
                        if (a.Branch != b.Branch)
                            return -(branchFirsts[a.Branch] - branchFirsts[b.Branch]);
                        return -a.Version.CompareTo(b.Version);
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
                    NameDialog = "updater_src_travis",

                    Index = "https://ams3.digitaloceanspaces.com/lollyde/everest-travis/builds_index.txt",

                    IsCurrent = () => VersionSuffix.StartsWith("travis-"),

                    ParseLine = CommonLineParser("https://ams3.digitaloceanspaces.com")
                },

                // TODO: GitHub updater source.
            };

            public static Task RequestAll() {
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
                    all.Sort((a, b) => {
                        return -a.Version.CompareTo(b.Version);
                    });
                    Newest = all[0];
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

                    Version version;
                    if (split.Length == 3)
                        version = new Version(split[2]);
                    else
                        version = new Version(0, 0, int.Parse(Regex.Match(split[1], @"\d+").Value));

                    return new Entry(name, branch, url, version);
                };

            public static Entry Newest { get; internal set; }
            public static bool HasUpdate => Newest != null && Newest.Version > Version;

            public static void Update(OuiLoggedProgress progress, Entry version = null) {
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
                const string errorHint = "\nPlease create a new issue on GitHub @ https://github.com/EverestAPI/Everest\nor join the #game_modding channel on Discord (invite in the repo).\nMake sure to upload your log.txt";

                // Check if we're on an OS which supports manipulating Celeste.exe while it's used.
                bool canModWhileAlive =
                    Environment.OSVersion.Platform == PlatformID.Unix ||
                    Environment.OSVersion.Platform == PlatformID.MacOSX;

                string zipPath = Path.Combine(PathGame, "everest-update.zip");
                string extractedPath = canModWhileAlive ? PathGame : Path.Combine(PathGame, "everest-update");

                progress.LogLine($"Updating to {version.Name} (branch: {version.Branch}) @ {version.URL}");

                progress.LogLine($"Downloading");
                DateTime timeStart = DateTime.Now;
                try {
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);

                    // Manual buffered copy from web input to file output.
                    // Allows us to measure speed and progress.
                    using (WebClient wc = new WebClient())
                    using (Stream input = wc.OpenRead(version.URL))
                    using (FileStream output = File.OpenWrite(zipPath))  {
                        long length;
                        if (input.CanSeek) {
                            length = input.Length;
                        } else {
                            length = _ContentLength(version.URL);
                        }
                        progress.Progress = 0;
                        progress.ProgressMax = (int) length;

                        byte[] buffer = new byte[4096];
                        DateTime timeLastSpeed = timeStart;
                        int read;
                        int readForSpeed = 0;
                        int pos = 0;
                        int speed = 0;
                        TimeSpan td;
                        while (pos < length) {
                            read = input.Read(buffer, 0, (int) Math.Min(buffer.Length, length - pos));
                            output.Write(buffer, 0, read);
                            pos += read;
                            readForSpeed += read;

                            td = DateTime.Now - timeLastSpeed;
                            if (td.TotalMilliseconds > 100) {
                                speed = (int) ((readForSpeed / 1024D) / td.TotalSeconds);
                                readForSpeed = 0;
                                timeLastSpeed = DateTime.Now;
                            }

                            progress.Lines[progress.Lines.Count - 1] =
                                $"Downloading: {((int) Math.Floor(100D * (pos / (double) length)))}% @ {speed} KiB/s";
                            progress.Progress = pos;
                        }
                    }
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

                            string fullPath = Path.Combine(extractedPath, entry.FileName);
                            string fullDir = Path.GetDirectoryName(fullPath);
                            if (!Directory.Exists(fullDir))
                                Directory.CreateDirectory(fullDir);
                            if (File.Exists(fullPath))
                                File.Delete(fullPath);
                            progress.LogLine($"{entry.FileName} -> {fullPath}");
                            entry.Extract(extractedPath); // Confusingly enough, this takes the base directory.
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

                // Load MiniInstaller and run it in a new app domain on systems supporting this.
                if (canModWhileAlive) {
                    progress.LogLine("Starting MiniInstaller");
                    progress.Progress = 0;
                    progress.ProgressMax = 0;
                    Directory.SetCurrentDirectory(PathGame);

                    try {
                        AppDomainSetup nestInfo = new AppDomainSetup();
                        nestInfo.ApplicationBase = Path.GetDirectoryName(extractedPath);

                        AppDomain nest = AppDomain.CreateDomain(
                            AppDomain.CurrentDomain.FriendlyName + " - MiniInstaller",
                            AppDomain.CurrentDomain.Evidence,
                            nestInfo,
                            AppDomain.CurrentDomain.PermissionSet
                        );

                        // nest.DoCallBack(Boot);
                        ((MiniInstallerProxy) nest.CreateInstanceAndUnwrap(
                            typeof(MiniInstallerProxy).Assembly.FullName,
                            typeof(MiniInstallerProxy).FullName
                        )).Boot(new MiniInstallerBridge {
                            Progress = progress,
                            ExtractedPath = extractedPath
                        });

                        AppDomain.Unload(nest);
                    } catch (Exception e) {
                        progress.LogLine("MiniInstaller failed!");
                        e.LogDetailed();
                        progress.LogLine(errorHint);
                        progress.Progress = 0;
                        progress.ProgressMax = 1;
                        return;
                    }
                }

                progress.Progress = 1;
                progress.ProgressMax = 1;
                progress.LogLine("Restarting");
                for (int i = 5; i > 0; --i) {
                    progress.Lines[progress.Lines.Count - 1] = $"Restarting in {i}";
                    Thread.Sleep(1000);
                }
                progress.Lines[progress.Lines.Count - 1] = $"Restarting";

                // Start MiniInstaller in a separate process on systems that don't support modding the game while it'S alive.
                if (!canModWhileAlive) {
                    try {
                        // We're on Windows or another OS which doesn't support manipulating Celeste.exe while it's used.
                        // Run MiniInstaller "out of body."
                        Process installer = new Process();
                        if (Type.GetType("Mono.Runtime") != null) {
                            installer.StartInfo.FileName = "mono";
                            installer.StartInfo.Arguments = "\"" + Path.Combine(extractedPath, "MiniInstaller.exe") + "\"";
                        } else {
                            installer.StartInfo.FileName = Path.Combine(extractedPath, "MiniInstaller.exe");
                        }
                        installer.StartInfo.WorkingDirectory = extractedPath;
                        installer.Start();
                    } catch (Exception e) {
                        progress.LogLine("Starting installer failed!");
                        e.LogDetailed();
                        progress.LogLine(errorHint);
                        progress.Progress = 0;
                        progress.ProgressMax = 1;
                    }

                } else {
                    // On Linux / macOS, restart the game after it shuts down.
                    Events.Celeste.OnShutdown += () => {
                        Process game = new Process();
                        // If the game was installed via Steam, it should restart in a Steam context on its own.
                        if (Environment.OSVersion.Platform == PlatformID.Unix ||
                            Environment.OSVersion.Platform == PlatformID.MacOSX) {
                            // The Linux and macOS versions come with a wrapping bash script.
                            game.StartInfo.FileName = "bash";
                            game.StartInfo.Arguments = "\"" + Path.Combine(PathGame, "Celeste") + "\"";
                        } else {
                            game.StartInfo.FileName = Path.Combine(PathGame, "Celeste.exe");
                        }
                        game.StartInfo.WorkingDirectory = PathGame;
                        game.Start();
                    };
                }
            }

            private static long _ContentLength(string url) {
                HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
                request.Method = "HEAD";
                using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
                    return response.ContentLength;
            }

            class MiniInstallerProxy : MarshalByRefObject {
                public void Boot(MiniInstallerBridge bridge) {
                    Assembly installerAssembly = Assembly.LoadFrom(Path.Combine(bridge.ExtractedPath, "MiniInstaller.exe"));
                    Type installerType = installerAssembly.GetType("MiniInstaller.Program");

                    // Set up any fields which we can set up by Everest.
                    /*
                    FieldInfo f_AsmMonoMod = installerType.GetField("AsmMonoMod");
                    if (f_AsmMonoMod != null)
                        f_AsmMonoMod.SetValue(null, typeof(MonoModder).Assembly);
                    */
                    FieldInfo f_LineLogger = installerType.GetField("LineLogger");
                    if (f_LineLogger != null)
                        // f_LineLogger.SetValue(null, new Action<string>(_ => progress.LogLine(_)).CastDelegate(f_LineLogger.FieldType));
                        f_LineLogger.SetValue(null, new Action<string>(bridge.LogLine).CastDelegate(f_LineLogger.FieldType));


                    // Let's just run the mod installer... from our mod... while we're running the mod...
                    object exitObject = installerAssembly.EntryPoint.Invoke(null, new object[] { new string[] { } });
                    if (exitObject != null && exitObject is int && ((int) exitObject) != 0)
                        throw new Exception($"Return code != 0, but {exitObject}");
                }
            }

            class MiniInstallerBridge : MarshalByRefObject {
                internal volatile OuiLoggedProgress Progress;
                public string ExtractedPath { get; set; }
                public void LogLine(string line) {
                    Progress.LogLine(line);
                }
            }

        }
    }
}
