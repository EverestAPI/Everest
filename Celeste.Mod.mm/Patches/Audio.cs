#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0414 // The field is assigned to, but never used

using Celeste.Mod;
using Celeste.Mod.Core;
using FMOD;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using SDL2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Celeste {
    static class patch_Audio {

        private static FMOD.Studio.System system;
        private static bool ready;
        private static FMOD.Studio._3D_ATTRIBUTES attributes3d;
        public static FMOD.Studio.System System => system;

        public static Dictionary<Guid, string> cachedPaths = new Dictionary<Guid, string>();
        public static Dictionary<Guid, string> cachedBankPaths = new Dictionary<Guid, string>();
        public static Dictionary<string, EventDescription> cachedModEvents = new Dictionary<string, EventDescription>();

        private static int modBankHandleLast = 0x0ade;
        private static Dictionary<IntPtr, ModAsset> modBankAssets = new Dictionary<IntPtr, ModAsset>();
        private static Dictionary<IntPtr, Stream> modBankStreams = new Dictionary<IntPtr, Stream>();

        private static Dictionary<string, HashSet<Guid>> usedGuids = new Dictionary<string, HashSet<Guid>>();

        private static HashSet<string> ingestedModBankPaths = new HashSet<string>();
        public static bool AudioInitialized { get; private set; } = false;

        [MonoModReplace]
        internal static void CheckFmod(RESULT result) {
            if (result != RESULT.OK)
                throw new Exception($"FMOD Failed: {result} ({Error.String(result)})");
        }

        [MonoModIfFlag("RelinkXNA")]
        [DllImport("fmod_SDL", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FMOD_SDL_Register(IntPtr system);

        [MonoModReplace]
        public static void Init() {
            bool fmodLiveUpdate = Settings.Instance.LaunchWithFMODLiveUpdate;
            Settings.Instance.LaunchWithFMODLiveUpdate |= CoreModule.Settings.LaunchWithFMODLiveUpdateInEverest;
        
            // Original initialization code
            {
                FMOD.Studio.INITFLAGS flags = FMOD.Studio.INITFLAGS.NORMAL;
                if (Settings.Instance.LaunchWithFMODLiveUpdate)
                    flags = FMOD.Studio.INITFLAGS.LIVEUPDATE;

                CheckFmod(FMOD.Studio.System.create(out system));

                // The following snippet is missing on XNA
                system.getLowLevelSystem(out var lowLevelSystem);
                if (SDL.SDL_GetPlatform().Equals("Linux"))
                    FMOD_SDL_Register(lowLevelSystem.getRaw());

                CheckFmod(system.initialize(1024, flags, FMOD.INITFLAGS.NORMAL, IntPtr.Zero));

                attributes3d.forward = new VECTOR { x = 0f, y = 0f, z = 1f };
                attributes3d.up = new VECTOR { x = 0f, y = 1f, z = 0f };
                Audio.SetListenerPosition(new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, -345f));

                ready = true;
            }

            Settings.Instance.LaunchWithFMODLiveUpdate = fmodLiveUpdate;

            // Original code loads audio banks in GameLoader.LoadThread.
            Audio.Banks.Master = Audio.Banks.Load("Master Bank", true);
            Audio.Banks.Music = Audio.Banks.Load("music", false);
            Audio.Banks.Sfxs = Audio.Banks.Load("sfx", false);
            Audio.Banks.UI = Audio.Banks.Load("ui", false);
            Audio.Banks.DlcMusic = Audio.Banks.Load("dlc_music", false);
            Audio.Banks.DlcSfxs = Audio.Banks.Load("dlc_sfx", false);

            // Prepopulate cachedPaths and usedGuids as both are being used directly.
            foreach (Bank bank in patch_Banks.Banks.Values) {
                bank.getEventList(out EventDescription[] descs);
                foreach (EventDescription desc in descs) {
                    if (!desc.isValid())
                        continue;
                    desc.getID(out Guid id);
                    desc.getPath(out string path);
                    cachedPaths[id] = path;
                    usedGuids[path] = new HashSet<Guid>() {
                        id
                    };
                }
            }

            // Load any additional banks.
            lock (Everest.Content.Map) {
                foreach (ModAsset asset in Everest.Content.Map.Values.Where(asset => asset.Type == typeof(AssetTypeBank))) {
                    if (!ingestedModBankPaths.Contains(asset.PathVirtual)) {
                        IngestBank(asset);
                    }
                }
            }

            AudioInitialized = true;
        }

        public static void IngestNewBanks() {
            lock (Everest.Content.Map) {
                foreach (ModAsset asset in Everest.Content.Map.Values.Where(asset => asset.Type == typeof(AssetTypeBank))) {
                    if (!ingestedModBankPaths.Contains(asset.PathVirtual)) {
                        IngestBank(asset);
                    }
                }
            }
        }

        [MonoModReplace]
        public static void Unload() {
            if (system == null)
                return;

            // Vanilla only calls unloadAll.
            // Avoid CheckFMOD as unload can happen after failed init.
            system.unloadAll();
            system.release();
            system = null;
            ready = false;
        }

        /// <summary>
        /// Loads an FMOD Bank from the given asset.
        /// </summary>
        public static Bank IngestBank(ModAsset asset) {
            Logger.Verbose("Audio.IngestBank", asset.PathVirtual);
            ingestedModBankPaths.Add(asset.PathVirtual);

            Bank bank;
            if (patch_Banks.ModCache.TryGetValue(asset, out bank))
                return bank;

            RESULT loadResult;
            if (CoreModule.Settings.UnpackFMODBanks) {
                loadResult = system.loadBankFile(asset.GetCachedPath(), LOAD_BANK_FLAGS.NORMAL, out bank);

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

                loadResult = system.loadBankCustom(info, LOAD_BANK_FLAGS.NORMAL, out bank);
            }

            if (loadResult == RESULT.ERR_EVENT_ALREADY_LOADED) {
                Logger.Warn("Audio.IngestBank", $"Cannot load {asset.PathVirtual} due to conflicting events!");
                return null;
            }

            loadResult.CheckFMOD();

            if (Everest.Content.TryGet<AssetTypeGUIDs>(asset.PathVirtual + ".guids", out ModAsset assetGUIDs)) {
                IngestGUIDs(assetGUIDs);
            }

            patch_Banks.ModCache[asset] = bank;

            bank.getID(out Guid id);
            cachedBankPaths[id] = $"bank:/mods/{asset.PathVirtual["Audio/".Length..]}";
            return bank;
        }

        /// <summary>
        /// Loads an FMOD GUID table from the given asset.
        /// </summary>
        public static void IngestGUIDs(ModAsset asset) {
            Logger.Verbose("Audio.IngestGUIDs", asset.PathVirtual);
            using (Stream stream = asset.Stream)
            using (StreamReader reader = new StreamReader(asset.Stream)) {
                while (reader.Peek() != -1) {
                    var line = reader.ReadLine().AsSpan().Trim("\r\n").Trim();

                    int indexOfSpace = line.IndexOf(' ');
                    if (indexOfSpace == -1)
                        continue;

                    if (!Guid.TryParse(line[..indexOfSpace], out Guid id) || cachedPaths.ContainsKey(id))
                        continue;

                    // only ingest the GUID if the corresponding event exists.
                    if (system.getEventByID(id, out EventDescription _event) > RESULT.OK)
                        continue;

                    string path = line[(indexOfSpace + 1)..].ToString();
                    if (!usedGuids.TryGetValue(path, out HashSet<Guid> used))
                        usedGuids[path] = used = new HashSet<Guid>();
                    if (!used.Add(id))
                        continue;

                    _event.unloadSampleData();
                    cachedPaths[id] = path;
                    cachedModEvents[path] = _event;

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
                status = system.getEventByID(Guid.Parse(path.AsSpan(7)), out desc);

            } else {
                status = system.getEvent(path, out desc);
            }

            if (status == RESULT.OK) {
                desc.loadSampleData();
                Audio.cachedEventDescriptions.Add(path, desc);

            } else if (status == RESULT.ERR_EVENT_NOTFOUND) {
                if (path is not ("null" or "event:/none")) {
                    Logger.Warn("Audio", $"Event not found: {path}");
                }

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

        public static Dictionary<string, Bank> Banks => patch_Audio.patch_Banks.Banks;

        /// <summary>
        /// Checks if the given FMOD result is RESULT.OK. Throws otherwise.
        /// </summary>
        public static void CheckFMOD(this RESULT result)
            => patch_Audio.CheckFmod(result);

        /// <inheritdoc cref="patch_Audio.IngestBank(ModAsset)"/>
        public static Bank IngestBank(ModAsset asset)
            => patch_Audio.IngestBank(asset);

        /// <inheritdoc cref="patch_Audio.IngestGUIDs(ModAsset)"/>
        public static void IngestGUIDs(ModAsset asset)
            => patch_Audio.IngestGUIDs(asset);

    }
}
