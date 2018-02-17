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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Updater {

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

                public string ErrorTitle { get; protected set; }
                public string Error { get; protected set; }

                private Task<Source> _RequestTask;
                public Task<Source> Request() {
                    if (_RequestTask != null)
                        return _RequestTask;
                    return _RequestTask = _RequestStart();
                }
                private async Task<Source> _RequestStart() {
                    Entries = null;
                    ErrorTitle = null;
                    Error = null;

                    string data;
                    using (WebClient wc = new WebClient()) {
                        try {
                           data  = await wc.DownloadStringTaskAsync(Index);
                        } catch (Exception e) {
                            ErrorTitle = "updater_versions_err_download";
                            Error = e.ToString();
                            return this;
                        }
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
                            ErrorTitle = "updater_versions_err_format";
                            Error = e.ToString();
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
                    ErrorTitle = null;
                    Error = null;
                }

            }

            public static List<Source> Sources = new List<Source>() {
                new Source {
                    NameDialog = "updater_src_travis",

                    Index = "https://ams3.digitaloceanspaces.com/lollyde/everest-travis/builds_index.txt",

                    IsCurrent = () => VersionSuffix.StartsWith("travis-"),

                    ParseLine = CommonLineParser("https://ams3.digitaloceanspaces.com/")
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
                        version = new Version(0, 0, 0, int.Parse(Regex.Match(split[1], @"\d+").Value));

                    return new Entry(name, branch, url, version);
                };

            public static Entry Newest { get; internal set; }
            public static bool HasUpdate => Newest != null && Newest.Version > Version;

        }
    }
}
