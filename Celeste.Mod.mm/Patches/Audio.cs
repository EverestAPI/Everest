#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used
#pragma warning disable CS0414 // The field is assigned to, but never used

using Celeste.Mod;
using Celeste.Mod.Core;
using FMOD;
using FMOD.Studio;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    static class patch_Audio {

        private static FMOD.Studio.System system;
        private static bool ready;
        public static FMOD.Studio.System System => system;

        public static Dictionary<Guid, string> cachedPaths = new Dictionary<Guid, string>();
        public static Dictionary<Guid, string> cachedBankPaths = new Dictionary<Guid, string>();
        public static Dictionary<string, EventDescription> cachedModEvents = new Dictionary<string, EventDescription>();

        private static int modBankHandleLast = 0x0ade;
        private static Dictionary<IntPtr, ModAsset> modBankAssets = new Dictionary<IntPtr, ModAsset>();
        private static Dictionary<IntPtr, Stream> modBankStreams = new Dictionary<IntPtr, Stream>();

        [MonoModIgnore]
        internal static extern void CheckFmod(RESULT result);

        public static extern void orig_Init();
        public static void Init() {
            bool fmodLiveUpdate = Settings.Instance.LaunchWithFMODLiveUpdate;
            Settings.Instance.LaunchWithFMODLiveUpdate |= CoreModule.Settings.LaunchWithFMODLiveUpdateInEverest;

            orig_Init();

            Settings.Instance.LaunchWithFMODLiveUpdate = fmodLiveUpdate;

            // Original code loads audio banks in GameLoader.LoadThread.
            Audio.Banks.Master = Audio.Banks.Load("Master Bank", true);
            Audio.Banks.Music = Audio.Banks.Load("music", false);
            Audio.Banks.Sfxs = Audio.Banks.Load("sfx", false);
            Audio.Banks.UI = Audio.Banks.Load("ui", false);
            Audio.Banks.DlcMusic = Audio.Banks.Load("dlc_music", false);
            Audio.Banks.DlcSfxs = Audio.Banks.Load("dlc_sfx", false);

            // Prepopulate cachedPaths, as it's being used directly.
            foreach (Bank bank in patch_Banks.Banks.Values) {
                EventDescription[] descs;
                bank.getEventList(out descs);
                foreach (EventDescription desc in descs) {
                    if (!desc.isValid())
                        continue;
                    desc.getID(out Guid id);
                    desc.getPath(out string path);
                    cachedPaths[id] = path;
                }
            }

            // Load any additional banks.
            lock (Everest.Content.Map) {
                foreach (ModAsset asset in Everest.Content.Map.Values.Where(asset => asset.Type == typeof(AssetTypeBank)))
                    IngestBank(asset);
            }
        }

        [MonoModReplace]
        public static void Unload() {
            if (system == null)
                return;

            // Vanilla only calls unloadAll.
            system.unloadAll().CheckFMOD();
            system.release().CheckFMOD();
            system = null;
            ready = false;
        }

        public static Bank IngestBank(ModAsset asset) {
            Logger.Log(LogLevel.Verbose, "Audio.IngestBank", asset.PathVirtual);

            Bank bank;
            if (patch_Banks.ModCache.TryGetValue(asset, out bank))
                return bank;

            if (CoreModule.Settings.UnpackFMODBanks) {
                system.loadBankFile(asset.GetCachedPath(), LOAD_BANK_FLAGS.NORMAL, out bank).CheckFMOD();

            } else {
                IntPtr handle;
                modBankAssets[handle = (IntPtr) (++modBankHandleLast)] = asset;
                BANK_INFO info = new BANK_INFO() {
                    size = patch_Banks.SizeOfBankInfo,

                    userdata = handle,
                    userdatalength = 0,

                    opencallback = ModBankOpen,
                    closecallback = ModBankClose,
                    readcallback = ModBankRead,
                    seekcallback = ModBankSeek
                };

                system.loadBankCustom(info, LOAD_BANK_FLAGS.NORMAL, out bank).CheckFMOD();
            }

            ModAsset assetGUIDs;
            if (Everest.Content.TryGet<AssetTypeGUIDs>(asset.PathVirtual + ".guids", out assetGUIDs)) {
                IngestGUIDs(assetGUIDs);
            }

            patch_Banks.ModCache[asset] = bank;

            bank.getID(out Guid id);
            cachedBankPaths[id] = $"bank:/mods/{asset.PathVirtual.Substring("Audio/".Length)}";
            return bank;
        }

        public static void IngestGUIDs(ModAsset asset) {
            Logger.Log(LogLevel.Verbose, "Audio.IngestGUIDs", asset.PathVirtual);
            using (Stream stream = asset.Stream)
            using (StreamReader reader = new StreamReader(asset.Stream)) {
                string line;
                while (reader.Peek() != -1) {
                    line = reader.ReadLine().Trim('\r', '\n').Trim();

                    int indexOfSpace = line.IndexOf(' ');
                    if (indexOfSpace == -1)
                        continue;

                    if (!Guid.TryParse(line.Substring(0, indexOfSpace), out Guid id) ||
                        cachedPaths.ContainsKey(id))
                        continue;
                    string path = line.Substring(indexOfSpace + 1);

                    EventDescription _event;
                    if (system.getEventByID(id, out _event) <= RESULT.OK) {
                        _event.unloadSampleData();
                        cachedPaths[id] = path;
                        cachedModEvents[path] = _event;
                    }
                    // TODO: Ingest buses and vcas
                }
            }
        }

        public static extern void orig_ReleaseUnusedDescriptions();
        public static void ReleaseUnusedDescriptions() {
            if (!CoreModule.Settings.UnloadUnusedAudio)
                return;
            orig_ReleaseUnusedDescriptions();
        }


        [MonoModReplace]
        public static string GetEventName(EventInstance instance) {
            if (instance == null)
                return "";

            instance.getDescription(out EventDescription desc);
            if (desc == null)
                return "";

            return GetEventName(desc);
        }

        public static string GetEventName(EventDescription desc) {
            if (desc == null)
                return "";

            desc.getID(out Guid id);

            if (cachedPaths.TryGetValue(id, out string path))
                return path;

            desc.getPath(out path);
            if (string.IsNullOrEmpty(path))
                path = "guid://" + id.ToString();
            return cachedPaths[id] = path;
        }

        public static string GetBankName(Bank bank) {
            if (bank == null)
                return "";

            bank.getID(out Guid id);

            if (cachedBankPaths.TryGetValue(id, out string path))
                return path;

            bank.getPath(out path);
            return cachedBankPaths[id] = path;
        }

        [MonoModReplace]
        public static EventDescription GetEventDescription(string path) {
            EventDescription desc = null;
            if (path == null || Audio.cachedEventDescriptions.TryGetValue(path, out desc))
                return desc;

            RESULT status;

            if (cachedModEvents.TryGetValue(path, out desc)) {
                status = RESULT.OK;

            } else if (path.StartsWith("guid://")) {
                status = system.getEventByID(new Guid(path.Substring(7)), out desc);

            } else {
                status = system.getEvent(path, out desc);
            }

            if (status == RESULT.OK) {
                desc.loadSampleData();
                Audio.cachedEventDescriptions.Add(path, desc);

            } else if (status == RESULT.ERR_EVENT_NOTFOUND) {
                Logger.Log("Audio", $"Event not found: {path}");

            } else {
                throw new Exception("FMOD getEvent failed: " + status);
            }

            return desc;
        }

        public static class patch_Banks {

            public static Dictionary<string, Bank> Banks = new Dictionary<string, Bank>();
            public static Dictionary<ModAsset, Bank> ModCache = new Dictionary<ModAsset, Bank>();

            public readonly static int SizeOfBankInfo = Marshal.SizeOf(typeof(BANK_INFO));

            [MonoModReplace]
            public static Bank Load(string name, bool loadStrings) {
                if (Banks == null)
                    Banks = new Dictionary<string, Bank>();

                Bank bank;
                if (Banks.TryGetValue(name, out bank))
                    return bank;

                ModAsset asset;
                if (Everest.Content.TryGet<AssetTypeBank>($"Audio/{name}", out asset)) {
                    bank = IngestBank(asset);

                } else {
                    system.loadBankFile(
                        Path.Combine(Engine.ContentDirectory, "FMOD", "Desktop", name + ".bank"),
                        LOAD_BANK_FLAGS.NORMAL, out bank
                    ).CheckFMOD();
                }

                if (loadStrings) {
                    if (Everest.Content.TryGet<AssetTypeBank>($"Audio/{name}.strings", out asset)) {
                        IngestBank(asset);
                    } else {
                        Bank strings;
                        system.loadBankFile(
                            Path.Combine(Engine.ContentDirectory, "FMOD", "Desktop", name + ".strings.bank"),
                            LOAD_BANK_FLAGS.NORMAL, out strings
                        ).CheckFMOD();
                    }
                }

                return Banks[name] = bank;
            }

        }

        private readonly static FILE_OPENCALLBACK ModBankOpen = (StringWrapper name, ref uint filesize, ref IntPtr handle, IntPtr userdata) => {
            Stream stream = modBankAssets[userdata].Stream;
            filesize = (uint) stream.Length;
            modBankStreams[handle = (IntPtr) (++modBankHandleLast)] = stream;
            return RESULT.OK;
        };

        private readonly static FILE_CLOSECALLBACK ModBankClose = (IntPtr handle, IntPtr userdata) => {
            modBankStreams[handle].Dispose();
            modBankStreams[handle] = null;
            return RESULT.OK;
        };

        private readonly static FILE_READCALLBACK ModBankRead = (IntPtr handle, IntPtr buffer, uint sizebytes, ref uint bytesread, IntPtr userdata) => {
            bytesread = 0;

            Stream stream = modBankStreams[handle];
            byte[] tmp = new byte[Math.Min(65536, sizebytes)];
            int read;
            while ((read = stream.Read(tmp, 0, Math.Min(tmp.Length, (int) (sizebytes - bytesread)))) > 0) {
                Marshal.Copy(tmp, 0, (IntPtr) ((long) buffer + bytesread), read);
                bytesread += (uint) read;
            }

            if (bytesread < sizebytes)
                return RESULT.ERR_FILE_EOF;
            return RESULT.OK;
        };

        private readonly static FILE_SEEKCALLBACK ModBankSeek = (IntPtr handle, uint pos, IntPtr userdata) => {
            modBankStreams[handle].Seek(pos, SeekOrigin.Begin);
            return RESULT.OK;
        };

    }
    public static class AudioExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static Dictionary<string, Bank> Banks => patch_Audio.patch_Banks.Banks;

        /// <summary>
        /// Checks if the given FMOD result is RESULT.OK. Throws otherwise.
        /// </summary>
        public static void CheckFMOD(this RESULT result)
            => patch_Audio.CheckFmod(result);

        /// <summary>
        /// Loads an FMOD Bank from the given asset.
        /// </summary>
        public static Bank IngestBank(ModAsset asset)
            => patch_Audio.IngestBank(asset);

        /// <summary>
        /// Loads an FMOD GUID table from the given asset.
        /// </summary>
        public static void IngestGUIDs(ModAsset asset)
            => patch_Audio.IngestGUIDs(asset);

    }
}
