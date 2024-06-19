#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System.Runtime.InteropServices;

namespace Celeste {
    static class patch_UserIO {

        [MonoModIfFlag("RelinkXNA")]
        [MonoModReplace]
        private static string SavePath = GetSavePath("Saves"), BackupPath = GetSavePath("Backups");

        private static List<Tuple<EverestModule, byte[], byte[]>> savingModFileData;
        private static byte[] savingMouseBindingsData;

        private static Queue<Tuple<bool, bool>> QueuedSaves;
        public static bool SaveQueued => (QueuedSaves?.Count ?? 0) > 0;

        [MonoModIgnore]
        public static bool Saving { get; private set; }

        private static string GetSavePath(string dir) {
            string env = Environment.GetEnvironmentVariable("EVEREST_SAVEPATH");
            if (!string.IsNullOrEmpty(env))
                return Path.Combine(env, dir);

            try {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                    string home = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                    if (!string.IsNullOrEmpty(home))
                        return Path.Combine(home, "Celeste/" + dir);

                    home = Environment.GetEnvironmentVariable("HOME");
                    if (!string.IsNullOrEmpty(home))
                        return Path.Combine(home, ".local/share/Celeste/" + dir);
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    string home = Environment.GetEnvironmentVariable("HOME");
                    if (!string.IsNullOrEmpty(home))
                        return Path.Combine(home, "Library/Application Support/Celeste/" + dir);
                }
                
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir);
            } catch (NotSupportedException) {
                return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), dir);
            }
        }

        [MonoModIgnore]
        [PatchUserIOPaths]
        private static extern string GetHandle(string name);

        [MonoModIgnore]
        [PatchUserIOPaths]
        private static extern string GetBackupHandle(string name);

        public static string GetSaveFilePath(string name = null)
            => string.IsNullOrEmpty(name) ? Path.GetDirectoryName(GetSaveFilePath("dummy")) : GetHandle(name);

        [MonoModIgnore]
        [PatchSaveRoutine]
        private static extern IEnumerator SaveRoutine(bool file, bool settings);

        public static extern void orig_SaveHandler(bool file, bool settings);
        public static void SaveHandler(bool file, bool settings) {
            if (QueuedSaves == null)
                QueuedSaves = new Queue<Tuple<bool, bool>>();

            if (Saving)
                QueuedSaves.Enqueue(Tuple.Create(file, settings));

            orig_SaveHandler(file, settings);
        }

        [MonoModLinkFrom("System.Collections.IEnumerator Celeste.UserIO::SaveHandler(System.Boolean,System.Boolean)")]
        public static IEnumerator SaveHandlerLegacy(bool file, bool settings) {
            // SaveHandler sets Celeste.SaveRoutine to an independently updated coroutine.
            // The caller still expects a "blocking" IEnumerator though.
            UserIO.SaveHandler(file, settings);
            return SaveNonHandler();
        }

        private static IEnumerator SaveNonHandler() {
            while (Saving)
                yield break;
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

            if (savingMouseBindingsData != null) {
                Save<VanillaMouseBindings>("modsettings-Everest_MouseBindings", savingMouseBindingsData);
                savingMouseBindingsData = null;
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
            if ((CoreModule.Settings.SaveDataFlush ?? true) && !MainThreadHelper.IsMainThread)
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
                    if (CoreModule.Settings.SaveDataFlush ?? false)
                        mod.ForceSaveDataFlush += 2;
                    mod.SaveSaveData(SaveData.Instance.FileSlot);
                    mod.SaveSession(SaveData.Instance.FileSlot);
#pragma warning restore CS0618
                }
            }

            SaveData.Instance.AfterInitialize();
        }

        private static void _SerializeMouseBindings() {
            savingMouseBindingsData = UserIO.Serialize(new VanillaMouseBindings().Init());
        }

        // Used where BeforeSave was previously used to enforce mod saving.
        internal static void ForceSerializeModSave() {
            UserIO.Save<ModSaveData>(SaveData.GetFilename(SaveData.Instance.FileSlot) + "-modsavedata", UserIO.Serialize(new ModSaveData((patch_SaveData) SaveData.Instance)));
            
            foreach (EverestModule mod in Everest._Modules) {
                if (CoreModule.Settings.SaveDataFlush ?? false)
                    mod.ForceSaveDataFlush += 2;
                if (mod.SaveDataAsync) {
                    mod.WriteSaveData(SaveData.Instance.FileSlot, mod.SerializeSaveData(SaveData.Instance.FileSlot));
                    mod.WriteSession(SaveData.Instance.FileSlot, mod.SerializeSession(SaveData.Instance.FileSlot));
                } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                    mod.SaveSaveData(SaveData.Instance.FileSlot);
                    mod.SaveSession(SaveData.Instance.FileSlot);
#pragma warning restore CS0618
                }
            }
        }

        internal static void _OnSaveRoutineEnd() {
            if (QueuedSaves.Count > 0) {
                Tuple<bool, bool> entry = QueuedSaves.Dequeue();
                SaveHandler(entry.Item1, entry.Item2);
            }
        }

        public static T Load<T>(string path) where T : class
            => UserIO.Load<T>(path, false);

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the UserIO.SaveRoutine method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchSaveRoutine))]
    class PatchSaveRoutineAttribute : Attribute { }

    /// <summary>
    /// Patches UserIO.Save to flush save data to disk after writing it.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchSaveDataFlushSaves))]
    class PatchSaveDataFlushSavesAttribute : Attribute { }

    /// <summary>
    /// Patches the method to use UserIO.SavePath/BackupPath instead of hardcoded constants on XNA.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchUserIOPaths))]
    class PatchUserIOPathsAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchSaveRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_SerializeModSave = method.DeclaringType.FindMethod("System.Void _SerializeModSave()");
            MethodDefinition m_SerializeMouseBindings = method.DeclaringType.FindMethod("System.Void _SerializeMouseBindings()");
            MethodDefinition m_OnSaveRoutineEnd = method.DeclaringType.FindMethod("System.Void _OnSaveRoutineEnd()");

            // The routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();

            new ILContext(method).Invoke(context => {
                ILCursor c = new ILCursor(context);

                // Insert After:
                // savingFileData = Serialize(SaveData.Instance);
                c.GotoNext(MoveType.After, instr => instr.MatchStsfld("Celeste.UserIO", "savingFileData"));
                c.Emit(OpCodes.Call, m_SerializeModSave);

                // Insert After:
                // savingSettingsData = Serialize(Settings.Instance);
                c.GotoNext(MoveType.After, instr => instr.MatchStsfld("Celeste.UserIO", "savingSettingsData"));
                c.Emit(OpCodes.Call, m_SerializeMouseBindings);

                // Insert at the end of the coroutine method
                c.GotoNext(MoveType.After, instr => instr.MatchStsfld("Celeste.Celeste", "SaveRoutine"));
                c.Emit(OpCodes.Call, m_OnSaveRoutineEnd);
            });
        }

        public static void PatchSaveDataFlushSaves(ILContext il, CustomAttribute attrib) {
            ILCursor c = new ILCursor(il);

            c.GotoNext(instr => instr.MatchCallvirt<Stream>("Write"));
            c.Next.OpCode = OpCodes.Call;
            c.Next.Operand = il.Method.DeclaringType.FindMethod("_saveAndFlush");

            // File.Copy(from, to, overwrite: true) => _saveAndFlushToFile(data, to)
            c.GotoNext(instr => instr.MatchCall(typeof(File), "Copy"));
            c.Index -= 3;

            // replace "from" with "data"
            c.Next.OpCode = OpCodes.Ldarg_1;
            // skip to "overwrite: true" and remove it
            c.Index += 2;
            c.Remove();
            // replace Files.Copy with _saveAndFlushToFile
            c.Next.OpCode = OpCodes.Call;
            c.Next.Operand = il.Method.DeclaringType.FindMethod("_saveAndFlushToFile");
        }

        public static void PatchUserIOPaths(ILContext il, CustomAttribute attrib) {
            // Patch UserIO.SavePath
            {
                ILCursor c = new ILCursor(il);
                FieldDefinition f_UserIO_SavePath = il.Method.DeclaringType.FindField("SavePath");
                while (c.TryGotoNext(MoveType.After, i => i.MatchLdstr("Saves"))) {
                    c.Instrs[c.Index-1].OpCode = OpCodes.Ldsfld;
                    c.Instrs[c.Index-1].Operand = f_UserIO_SavePath;
                }
            }

            // Patch UserIO.BackupPath
            {
                ILCursor c = new ILCursor(il);
                FieldDefinition f_UserIO_SavePath = il.Method.DeclaringType.FindField("BackupPath");
                while (c.TryGotoNext(MoveType.After, i => i.MatchLdstr("Backups"))) {
                    c.Instrs[c.Index-1].OpCode = OpCodes.Ldsfld;
                    c.Instrs[c.Index-1].Operand = f_UserIO_SavePath;
                }
            }
        }

    }
}
