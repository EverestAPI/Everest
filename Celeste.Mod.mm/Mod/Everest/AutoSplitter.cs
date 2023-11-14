
using Monocle;
using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Celeste.Mod {
    /// <summary>
    /// Manages the in-memory autosplitter info
    /// </summary>
    internal unsafe static class AutoSplitter {
#region Splitter Info
        [StructLayout(LayoutKind.Explicit)]
        internal struct CoreAutoSplitterInfo {
            public const byte CurrentVersion = 3;

            public const int MagicSize = 0x14;
            public static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("EVERESTAUTOSPLIT").Concat(new byte[] { 0xf0, 0xf1, 0xf2, 0xf3,  }).ToArray();

            static CoreAutoSplitterInfo() => Trace.Assert(MagicBytes.Length == MagicSize);

            //Info Header
            [FieldOffset(0x00)] public fixed byte Magic[MagicSize];
            [FieldOffset(0x14)] public byte CelesteVersionMajor;
            [FieldOffset(0x15)] public byte CelesteVersionMinor;
            [FieldOffset(0x16)] public byte CelesteVersionBuild;
            [FieldOffset(0x17)] public byte InfoVersion;
            [FieldOffset(0x18)] public nint EverestVersionStrPtr;

            //Chapter / Level Metadata
            [FieldOffset(0x20)] public nint LevelSetStrPtr;
            [FieldOffset(0x28)] public nint ChapterSIDStrPtr;
            [FieldOffset(0x30)] public int ChapterID;
            [FieldOffset(0x34)] public int ChapterMode;
            [FieldOffset(0x38)] public nint RoomNameStrPtr;

            //Chapter Progress
            [FieldOffset(0x40)] public long ChapterTime;
            [FieldOffset(0x48)] public int ChapterStrawberries;
            [FieldOffset(0x4c)] public AutoSplitterChapterFlags ChapterFlags;

            //File Progress
            [FieldOffset(0x50)] public long FileTime;
            [FieldOffset(0x58)] public int FileStrawberries;
            [FieldOffset(0x5c)] public int FileGoldenStrawberries;
            [FieldOffset(0x60)] public int FileCassettes;
            [FieldOffset(0x64)] public int FileHearts;
            [FieldOffset(0x68)] public AutoSplitterFileFlags FileFlags;
        }

        [Flags]
        internal enum AutoSplitterChapterFlags : uint {
            ChapterStarted  = 1U << 0,
            ChapterComplete = 1U << 1,
            ChapterCassette = 1U << 2,
            ChapterHeart    = 1U << 3,
            GrabbedGolden   = 1U << 4,

            TimerActive     = 1U << 31
        }

        [Flags]
        internal enum AutoSplitterFileFlags : uint {
            IsDebug         = 1U << 0,
            AssistMode      = 1U << 1,
            VariantsMode    = 1U << 2,

            StartingNewFile = 1U << 30,
            FileActive      = 1U << 31,
        }
#endregion

        private static bool _IsInitialized;
        private static MemoryMappedFile _SplitterInfoFile;
        private static MemoryMappedViewAccessor _SplitterInfoView, _StringPoolAView, _StringPoolBView;
        private static byte* _SplitterInfoPtr;
        private static byte* _StringPoolAPtr, _StringPoolBPtr;
        private static bool _UseStringPoolB;
        private static long _StringPoolOffset;

        internal static ref CoreAutoSplitterInfo SplitterInfo => ref Unsafe.AsRef<CoreAutoSplitterInfo>((void*) _SplitterInfoPtr);

        internal static void Init() {
            Trace.Assert(!_IsInitialized);

            const int PAGE_SIZE = 4096;

            // Initialize the memory mapped file
            _SplitterInfoFile = MemoryMappedFile.CreateNew(null, 3 * PAGE_SIZE);
            _SplitterInfoView = _SplitterInfoFile.CreateViewAccessor(0*PAGE_SIZE, PAGE_SIZE);
            _StringPoolAView =  _SplitterInfoFile.CreateViewAccessor(1*PAGE_SIZE, PAGE_SIZE);
            _StringPoolBView =  _SplitterInfoFile.CreateViewAccessor(2*PAGE_SIZE, PAGE_SIZE);

            // Acquire pointers
            _SplitterInfoView.SafeMemoryMappedViewHandle.AcquirePointer(ref _SplitterInfoPtr);
            _StringPoolAView.SafeMemoryMappedViewHandle.AcquirePointer(ref _StringPoolAPtr);
            _StringPoolBView.SafeMemoryMappedViewHandle.AcquirePointer(ref _StringPoolBPtr);

            // Initialize the header
            ref CoreAutoSplitterInfo info = ref SplitterInfo;

            // - magic bytes
            fixed (byte* magicSrcPtr = CoreAutoSplitterInfo.MagicBytes, magicDstPtr = info.Magic)
                Buffer.MemoryCopy(magicSrcPtr, magicDstPtr, CoreAutoSplitterInfo.MagicSize, CoreAutoSplitterInfo.MagicSize);

            // - version numbers
            info.CelesteVersionMajor = (byte) Celeste.Instance.Version.Major;
            info.CelesteVersionMinor = (byte) Celeste.Instance.Version.Minor;
            info.CelesteVersionBuild = (byte) Celeste.Instance.Version.Build;
            info.InfoVersion = CoreAutoSplitterInfo.CurrentVersion;

            // - Everest version string
            long strOff = Marshal.SizeOf<CoreAutoSplitterInfo>();
            info.EverestVersionStrPtr = (nint) (_SplitterInfoPtr + WriteString(_SplitterInfoView, ref strOff, Everest.VersionString));

            _IsInitialized = true;
        }

        internal static void Shutdown() {
            if (!_IsInitialized)
                return;

            // Release pointers
            if (_SplitterInfoPtr != null)
                _SplitterInfoView.SafeMemoryMappedViewHandle.ReleasePointer();
            if (_StringPoolAPtr != null)
                _StringPoolAView.SafeMemoryMappedViewHandle.ReleasePointer();
            if (_StringPoolBPtr != null)
                _StringPoolBView.SafeMemoryMappedViewHandle.ReleasePointer();

            // Dispose the memory mapped file
            _SplitterInfoView.Dispose();
            _StringPoolAView.Dispose();
            _StringPoolBView.Dispose();
            _SplitterInfoFile.Dispose();

            _IsInitialized = false;
        }

        private static void ResetStringPool() {
            _UseStringPoolB = !_UseStringPoolB;
            _StringPoolOffset = 0;
        }

        private static long WriteString(MemoryMappedViewAccessor view, ref long offset, string str) {
            //Strings are encoded as null-terminated UTF8 strings
            //Additionally, the length of the string is stored as a ushort right before the string data at offset -2
            byte[] utf8Str = Encoding.UTF8.GetBytes(str);
            view.Write(offset, (ushort) utf8Str.Length);
            view.WriteArray(offset + 2, utf8Str, 0, utf8Str.Length);
            view.Write(offset + 2 + utf8Str.Length, (byte) 0);

            long ptrOff = offset + 1;
            offset += 2 + utf8Str.Length + 1;
            return ptrOff;
        }

        private static nint AppendStringToPool(string str) {
            if (string.IsNullOrEmpty(str))
                return 0;

            long ptrOff = WriteString(_UseStringPoolB ? _StringPoolBView : _StringPoolAView, ref _StringPoolOffset, str);
            return (nint) ((_UseStringPoolB ? _StringPoolBPtr : _StringPoolAPtr) + ptrOff);
        }

        internal static void Update() {
            if (!_IsInitialized)
                return;

            ref CoreAutoSplitterInfo info = ref SplitterInfo;
            ResetStringPool();

            // Mark the splitter info as currently being updated
            info.InfoVersion = 0;
            Thread.MemoryBarrier();

            // Update chapter / level data
            if (Engine.Scene is Level lvl) {
                info.LevelSetStrPtr = AppendStringToPool(lvl.Session.Area.GetLevelSet());
                info.ChapterSIDStrPtr = AppendStringToPool(lvl.Session.Area.GetSID());
                info.ChapterID = lvl.Session.Area.ID;
                info.ChapterMode = (int) lvl.Session.Area.Mode;
                info.RoomNameStrPtr = AppendStringToPool(lvl.Session.Level);

                info.ChapterTime = lvl.Session.Time;
                info.ChapterStrawberries = lvl.Session.Strawberries.Count;
                info.ChapterFlags =
                    AutoSplitterChapterFlags.ChapterStarted |
                    (lvl.Completed ? AutoSplitterChapterFlags.ChapterComplete : 0) |
                    (lvl.Session.Cassette ? AutoSplitterChapterFlags.ChapterCassette : 0) |
                    (lvl.Session.HeartGem ? AutoSplitterChapterFlags.ChapterHeart : 0) |
                    (lvl.Session.GrabbedGolden ? AutoSplitterChapterFlags.GrabbedGolden : 0) |
                    (!lvl.Completed ? AutoSplitterChapterFlags.TimerActive : 0)
                ;
            } else {
                info.LevelSetStrPtr = 0;
                info.ChapterSIDStrPtr = 0;
                info.ChapterID = -1;
                info.ChapterMode = -1;
                info.RoomNameStrPtr = 0;

                info.ChapterTime = 0;
                info.ChapterStrawberries = 0;
                info.ChapterFlags = 0;
            }

            // Update save file info
            if (SaveData.Instance is SaveData saveData) {
                info.FileTime = saveData.Time;
                info.FileStrawberries = saveData.TotalStrawberries;
                info.FileGoldenStrawberries = saveData.TotalGoldenStrawberries;
                info.FileCassettes = saveData.TotalCassettes;
                info.FileHearts = saveData.TotalHeartGems;
                info.FileFlags = 
                    AutoSplitterFileFlags.FileActive |
                    (saveData.DebugMode ? AutoSplitterFileFlags.IsDebug : 0) |
                    (saveData.AssistMode ? AutoSplitterFileFlags.AssistMode : 0) |
                    (saveData.VariantMode ? AutoSplitterFileFlags.VariantsMode : 0)
                ;

                // Set a file flag when a new file has just been started
                if (Engine.Scene is Overworld ovw && ovw.GetUI<OuiFileSelect>() is patch_OuiFileSelect fileSelect && fileSelect.startingNewFile)
                    info.FileFlags |= AutoSplitterFileFlags.StartingNewFile;
            } else {
                info.FileTime = 0;
                info.FileStrawberries = 0;
                info.FileGoldenStrawberries = 0;
                info.FileCassettes = 0;
                info.FileHearts = 0;
                info.FileFlags = 0;
            }

            // Mark the splitter info as being valid again
            info.InfoVersion = CoreAutoSplitterInfo.CurrentVersion;
            Thread.MemoryBarrier();
        }
    }
}