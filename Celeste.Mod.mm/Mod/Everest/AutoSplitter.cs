
using Monocle;
using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Celeste.Mod {
    /// <summary>
    /// Manages the in-memory autosplitter info
    /// </summary>
    internal unsafe static class AutoSplitter {
#region Splitter Info
        [StructLayout(LayoutKind.Explicit)]
        internal struct AutoSplitterInfo {
            public const byte CurrentVersion = 1;

            public const int MagicSize = 0x14;
            public static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("EVERESTAUTOSPLIT").Concat(new byte[] { 0xf0, 0xf1, 0xf2, 0xf3,  }).ToArray();

            static AutoSplitterInfo() => Trace.Assert(MagicBytes.Length == MagicSize);

            [FieldOffset(0x00)] public fixed byte Magic[MagicSize];
            [FieldOffset(0x14)] public byte CelesteVersionMajor;
            [FieldOffset(0x15)] public byte CelesteVersionMinor;
            [FieldOffset(0x16)] public byte CelesteVersionBuild;
            [FieldOffset(0x17)] public byte InfoVersion;
            [FieldOffset(0x18)] public nint EverestVersionStrPtr;

            [FieldOffset(0x20)] public int Chapter;
            [FieldOffset(0x24)] public int Mode;
            [FieldOffset(0x28)] public nint LevelStrPtr;

            [FieldOffset(0x30)] public long ChapterTime;
            [FieldOffset(0x38)] public int ChapterStrawberries;
            [FieldOffset(0x3c)] public AutoSplitterChapterFlags ChapterFlags;

            [FieldOffset(0x40)] public long FileTime;
            [FieldOffset(0x48)] public int FileStrawberries;
            [FieldOffset(0x4c)] public int FileGoldenStrawberries;
            [FieldOffset(0x50)] public int FileCassettes;
            [FieldOffset(0x54)] public int FileHearts;
            [FieldOffset(0x58)] public AutoSplitterFileFlags FileFlags;
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
        internal enum AutoSplitterFileFlags : uint {}
#endregion

        private static bool _IsInitialized;
        private static MemoryMappedFile _SplitterInfoFile;
        private static MemoryMappedViewAccessor _SplitterInfoView, _StringPoolAView, _StringPoolBView;
        private static byte* _SplitterInfoPtr;
        private static byte* _StringPoolAPtr, _StringPoolBPtr;
        private static bool _UseStringPoolB;
        private static long _StringPoolOffset;

        internal static ref AutoSplitterInfo SplitterInfo => ref Unsafe.AsRef<AutoSplitterInfo>((void*) _SplitterInfoPtr);

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
            ref AutoSplitterInfo info = ref SplitterInfo;

            // - magic bytes
            fixed (byte* magicSrcPtr = AutoSplitterInfo.MagicBytes, magicDstPtr = info.Magic)
                Buffer.MemoryCopy(magicSrcPtr, magicDstPtr, AutoSplitterInfo.MagicSize, AutoSplitterInfo.MagicSize);

            // - version numbers
            info.CelesteVersionMajor = (byte) Celeste.Instance.Version.Major;
            info.CelesteVersionMinor = (byte) Celeste.Instance.Version.Minor;
            info.CelesteVersionBuild = (byte) Celeste.Instance.Version.Build;
            info.InfoVersion = AutoSplitterInfo.CurrentVersion;

            // - Everest version string
            long strOff = Marshal.SizeOf<AutoSplitterInfo>();
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
            view.Write(offset - 2, (ushort) utf8Str.Length);
            view.WriteArray(offset, utf8Str, 0, utf8Str.Length);
            view.Write(offset + utf8Str.Length, (byte) 0);

            long ptrOff = offset + 1;
            offset += 2 + utf8Str.Length + 1;
            return ptrOff;
        }

        private static nint AppendStringToPool(string str) {
            long ptrOff = WriteString(_UseStringPoolB ? _StringPoolBView : _StringPoolAView, ref _StringPoolOffset, str);
            return (nint) ((_UseStringPoolB ? _StringPoolBPtr : _StringPoolAPtr) + ptrOff);
        }

        internal static void Update() {
            if (!_IsInitialized)
                return;

            ref AutoSplitterInfo info = ref SplitterInfo;
            ResetStringPool();

            // Update chapter / level data
            if (Engine.Scene is Level lvl) {
                info.Chapter = lvl.Session.Area.ID;
                info.Mode = (int) lvl.Session.Area.Mode;
                info.LevelStrPtr = AppendStringToPool(lvl.Session.Level);

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
                info.Chapter = -1;
                info.Mode = -1;
                info.LevelStrPtr = AppendStringToPool(string.Empty);

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
                info.FileFlags = 0;
            } else {
                info.FileTime = 0;
                info.FileStrawberries = 0;
                info.FileGoldenStrawberries = 0;
                info.FileCassettes = 0;
                info.FileHearts = 0;
                info.FileFlags = 0;
            }
        }
    }
}