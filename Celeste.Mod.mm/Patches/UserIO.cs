#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace Celeste {
    static class patch_UserIO {

        private static List<Tuple<EverestModule, byte[], byte[]>> savingModFileData;

        [MonoModIgnore]
        public static bool Saving { get; private set; }

        private static extern string orig_GetSavePath(string dir);
        [MonoModIfFlag("FNA")]
        private static string GetSavePath(string dir) {
            string env = Environment.GetEnvironmentVariable("EVEREST_SAVEPATH");
            if (!string.IsNullOrEmpty(env))
                return Path.Combine(env, dir);

            try {
                return orig_GetSavePath(dir);
            } catch (NotSupportedException) {
                return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), dir);
            }
        }

        [MonoModIgnore]
        private static extern string GetHandle(string name);

        public static string GetSaveFilePath(string name = null)
            => string.IsNullOrEmpty(name) ? Path.GetDirectoryName(GetSaveFilePath("dummy")) : GetHandle(name);

        [MonoModIgnore]
        [PatchSaveRoutine]
        private static extern IEnumerator SaveRoutine(bool file, bool settings);

        [MonoModLinkFrom("System.Collections.IEnumerator Celeste.UserIO::SaveHandler(System.Boolean,System.Boolean)")]
        public static IEnumerator SaveHandlerLegacy(bool file, bool settings) {
            if (Saving)
                return SaveNonHandler();
            Saving = true;
            // Note how we're calling SaveRoutine, not orig_SaveHandler.
            return new SafeRoutine(SaveRoutine(file, settings));
        }


        private static extern void orig_SaveThread();
        private static void SaveThread() {
            if (savingModFileData != null) {
                foreach (Tuple<EverestModule, byte[], byte[]> data in savingModFileData) {
                    if (data.Item1 == null) {
                        UserIO.Save<ModSaveData>(SaveData.GetFilename(SaveData.Instance.FileSlot) + "-modsavedata", data.Item2);
                        continue;
                    }

                    data.Item1.WriteSaveData(SaveData.Instance.FileSlot, data.Item2);
                    data.Item1.WriteSession(SaveData.Instance.FileSlot, data.Item3);
                }
                savingModFileData = null;
            }

            orig_SaveThread();
        }

        // Patch the Deserialize method so that it doesn't use BinaryFormatter (that causes an arbitrary code execution vulnerability).
        [MonoModReplace]
        private static T Deserialize<T>(Stream stream) where T : class {
            return (T) new XmlSerializer(typeof(T)).Deserialize(stream);
        }

        public static extern T orig_Load<T>(string path, bool backup = false);
        public static T Load<T>(string path, bool backup = false) {
            T result = orig_Load<T>(path, backup);

            // if we are loading a SaveData, fill out the FileSlot right away.
            if (typeof(T) == typeof(SaveData) && result != null) {
                if (path == "debug") {
                    (result as SaveData).FileSlot = -1;
                } else if (int.TryParse(path, out int slot)) {
                    (result as SaveData).FileSlot = slot;
                }
            }

            return result;
        }

        [MonoModIgnore]
        [PatchSaveDataFlushSaves]
        public static extern bool Save<T>(string path, byte[] data);

        private static void _saveAndFlushToFile(byte[] data, string handle) {
            using (FileStream fileStream = File.Open(handle, FileMode.Create, FileAccess.Write)) {
                _saveAndFlush(fileStream, data, 0, data.Length);
            }
        }

        private static void _saveAndFlush(FileStream stream, byte[] array, int offset, int count) {
            stream.Write(array, offset, count);
            stream.Flush(true);
        }

        private static void _SerializeModSave() {
            savingModFileData = new List<Tuple<EverestModule, byte[], byte[]>>();
            savingModFileData.Add(Tuple.Create<EverestModule, byte[], byte[]>(
                null,
                UserIO.Serialize(new ModSaveData((patch_SaveData) SaveData.Instance)),
                null
            ));

            foreach (EverestModule mod in Everest._Modules) {
                if (mod.SaveDataAsync) {
                    savingModFileData.Add(Tuple.Create(
                        mod,
                        mod.SerializeSaveData(SaveData.Instance.FileSlot),
                        mod.SerializeSession(SaveData.Instance.FileSlot)
                    ));
                } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                    mod.SaveSaveData(SaveData.Instance.FileSlot);
                    mod.SaveSession(SaveData.Instance.FileSlot);
#pragma warning restore CS0618
                }
            }

            SaveData.Instance.AfterInitialize();
        }

        private static IEnumerator SaveNonHandler() {
            yield break;
        }

        public static T Load<T>(string path) where T : class
            => UserIO.Load<T>(path, false);

    }
}
