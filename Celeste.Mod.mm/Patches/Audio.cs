#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using FMOD;
using FMOD.Studio;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    static class patch_Audio {

        private static FMOD.Studio.System system;
        public static FMOD.Studio.System System => system;

        public static Dictionary<Guid, string> cachedPaths = new Dictionary<Guid, string>();

        [MonoModIgnore]
        internal static extern void CheckFmod(RESULT result);

        public static extern void orig_Init();
        public static void Init() {
            orig_Init();

            // Original code loads audio banks in GameLoader.LoadThread.
            Audio.Banks.Master = Audio.Banks.Load("Master Bank", true);
            Audio.Banks.Music = Audio.Banks.Load("music", false);
            Audio.Banks.Sfxs = Audio.Banks.Load("sfx", false);
            Audio.Banks.UI = Audio.Banks.Load("ui", false);

            foreach (ModAsset asset in Everest.Content.ListBanks) {
                IngestBank(asset);
            }
        }

        public static Bank IngestBank(ModAsset asset) {
            Logger.Log(LogLevel.Verbose, "Audio.IngestBank", asset.PathMapped);

            Bank bank;
            if (patch_Banks.ModCache.TryGetValue(asset, out bank))
                return bank;

            // TODO: Use loadBankCustom with BANK_INFO reading from stream.
            system.loadBankMemory(asset.Data, LOAD_BANK_FLAGS.NORMAL, out bank).CheckFMOD();

            ModAsset assetGUIDs;
            if (Everest.Content.TryGet<AssetTypeGUIDs>(asset.PathMapped + ".guids", out assetGUIDs)) {
                IngestGUIDs(assetGUIDs);
            }

            patch_Banks.ModCache[asset] = bank;
            return bank;
        }

        public static void IngestGUIDs(ModAsset asset) {
            Logger.Log(LogLevel.Verbose, "Audio.IngestGUIDs", asset.PathMapped);
            using (Stream stream = asset.Stream)
            using (StreamReader reader = new StreamReader(asset.Stream)) {
                string line;
                while (reader.Peek() != -1) {
                    line = reader.ReadLine().Trim('\r', '\n').Trim();

                    int indexOfSpace = line.IndexOf(' ');
                    if (indexOfSpace == -1)
                        continue;

                    Guid id;
                    if (!Guid.TryParse(line.Substring(0, indexOfSpace), out id) ||
                        cachedPaths.ContainsKey(id))
                        continue;
                    string path = line.Substring(indexOfSpace + 1);

                    EventDescription _event;
                    if (system.getEventByID(id, out _event) <= RESULT.OK) {
                        Audio.cachedEventDescriptions[path] = _event;
                        cachedPaths[id] = path;
                    }
                    // TODO: Ingest buses and vcas
                }
            }
        }

        [MonoModReplace]
        public static string GetEventName(EventInstance instance) {
            if (instance == null)
                return "";

            EventDescription desc;
            instance.getDescription(out desc);
            if (desc == null)
                return "";

            Guid id;
            desc.getID(out id);

            string path;
            if (cachedPaths.TryGetValue(id, out path))
                return path;

            desc.getPath(out path);
            return cachedPaths[id] = path;
        }

        public static class patch_Banks {

            public static Dictionary<string, Bank> Banks = new Dictionary<string, Bank>();
            public static Dictionary<ModAsset, Bank> ModCache = new Dictionary<ModAsset, Bank>();

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

    }
    public static class AudioExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static FMOD.Studio.System System => patch_Audio.System;

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
