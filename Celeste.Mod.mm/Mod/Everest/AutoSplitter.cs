
using Monocle;
using System;
using System.Buffers;
using System.Buffers.Binary;
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
    public static class AutoSplitter {
#region Splitter Info
        [StructLayout(LayoutKind.Explicit)]
        public unsafe struct CoreAutoSplitterInfo {
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
        public enum AutoSplitterChapterFlags : uint {
            ChapterStarted  = 1U << 0,
            ChapterComplete = 1U << 1,
            ChapterCassette = 1U << 2,
            ChapterHeart    = 1U << 3,
            GrabbedGolden   = 1U << 4,

            TimerActive     = 1U << 31
        }

        [Flags]
        public enum AutoSplitterFileFlags : uint {
            IsDebug         = 1U << 0,
            AssistMode      = 1U << 1,
            VariantsMode    = 1U << 2,

            StartingNewFile = 1U << 30,
            FileActive      = 1U << 31,
        }
#endregion

        private static bool _IsInitialized;
        private static MemoryMappedFile _SplitterInfoFile;
        private static InfoMemoryManager _SplitterInfoMemManager;
        private static Memory<byte> _SplitterInfoBuf, _StringPoolA, _StringPoolB;
        private static bool _UseStringPoolB;
        private static int _StringPoolOffset;

        internal static ref CoreAutoSplitterInfo SplitterInfo => ref Unsafe.As<byte, CoreAutoSplitterInfo>(ref _SplitterInfoBuf.Span[0]);

        internal static void Init() {
            Trace.Assert(!_IsInitialized);

            const int PAGE_SIZE = 4096;

            // Initialize the memory mapped file
            _SplitterInfoFile = MemoryMappedFile.CreateNew(null, 3 * PAGE_SIZE);
            _SplitterInfoMemManager = new InfoMemoryManager(_SplitterInfoFile);

            // Setup memory objects
            Memory<byte> mem = _SplitterInfoMemManager.Memory;
            _SplitterInfoBuf    = mem.Slice(0*PAGE_SIZE, PAGE_SIZE);
            _StringPoolB        = mem.Slice(2*PAGE_SIZE, PAGE_SIZE);
            _StringPoolA        = mem.Slice(1*PAGE_SIZE, PAGE_SIZE);

            // Initialize the header
            ref CoreAutoSplitterInfo info = ref SplitterInfo;

            // - magic bytes
            unsafe {
                fixed (byte* magicSrcPtr = CoreAutoSplitterInfo.MagicBytes, magicDstPtr = info.Magic)
                    Buffer.MemoryCopy(magicSrcPtr, magicDstPtr, CoreAutoSplitterInfo.MagicSize, CoreAutoSplitterInfo.MagicSize);
            }

            // - version numbers
            info.CelesteVersionMajor = (byte) Celeste.Instance.Version.Major;
            info.CelesteVersionMinor = (byte) Celeste.Instance.Version.Minor;
            info.CelesteVersionBuild = (byte) Celeste.Instance.Version.Build;
            info.InfoVersion = CoreAutoSplitterInfo.CurrentVersion;

            // - Everest version string
            int strOff = Marshal.SizeOf<CoreAutoSplitterInfo>();
            info.EverestVersionStrPtr = WriteString(_SplitterInfoBuf.Span, ref strOff, Everest.VersionString);

            _IsInitialized = true;
        }

        internal static void Shutdown() {
            if (!_IsInitialized)
                return;

            // Release buffers
            _SplitterInfoBuf = Memory<byte>.Empty;
            _StringPoolA = Memory<byte>.Empty;
            _StringPoolB = Memory<byte>.Empty;

            // Dispose the memory mapped file
            _SplitterInfoFile.Dispose();

            _IsInitialized = false;
        }

        private static void ResetStringPool() {
            _UseStringPoolB = !_UseStringPoolB;
            _StringPoolOffset = 0;
        }

        private static nint WriteString(Span<byte> mem, ref int offset, string str) {
            if (string.IsNullOrEmpty(str))
                return 0;

            //Strings are encoded as null-terminated UTF8 strings
            //Additionally, the length of the string is stored as a ushort right before the string data at offset -2
            byte[] utf8Str = Encoding.UTF8.GetBytes(str);

            mem = mem[offset..];
            offset += 2 + utf8Str.Length + 1;

            BinaryPrimitives.WriteUInt16LittleEndian(mem[..2], (ushort) utf8Str.Length);
            utf8Str.CopyTo(mem[2..(2+utf8Str.Length)]);
            mem[2+utf8Str.Length] = 0;

            unsafe {
                fixed (byte* memPtr = mem)
                    return (nint) (memPtr + 2);
            }
        }

        private static nint AppendStringToPool(string str) => WriteString(_UseStringPoolB ? _StringPoolB.Span : _StringPoolA.Span, ref _StringPoolOffset, str);

        public static void Update() {
            if (!_IsInitialized)
                return;

            ref CoreAutoSplitterInfo info = ref SplitterInfo;
            ResetStringPool();

            // Mark the splitter info as currently being updated
            info.InfoVersion = 0xff;
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

            // Run update event handlers
            OnUpdateInfo?.Invoke(ref info, AppendStringToPool);

            // Mark the splitter info as being valid again
            info.InfoVersion = CoreAutoSplitterInfo.CurrentVersion;
            Thread.MemoryBarrier();
        }

        public delegate void InfoUpdateHandler(ref CoreAutoSplitterInfo info, Func<string, nint> stringWriter);
        public static event InfoUpdateHandler OnUpdateInfo;

        private unsafe class InfoMemoryManager : MemoryManager<byte> {

            private MemoryMappedViewAccessor _SplitterInfoView;
            private byte* _SplitterInfoPtr;

            public InfoMemoryManager(MemoryMappedFile memFile) {
                _SplitterInfoView = memFile.CreateViewAccessor();

                _SplitterInfoPtr = null;
                _SplitterInfoView.SafeMemoryMappedViewHandle.AcquirePointer(ref _SplitterInfoPtr);
                _SplitterInfoPtr += _SplitterInfoView.PointerOffset;
            }

            public override Span<byte> GetSpan() {
                return new Span<byte>(_SplitterInfoPtr, (int) _SplitterInfoView.Capacity);
            }

            public override MemoryHandle Pin(int elementIndex = 0) {
                return new MemoryHandle(_SplitterInfoPtr + elementIndex);
            }

            public override void Unpin() {}

            protected override void Dispose(bool disposing) {
                if (_SplitterInfoPtr != null) {
                    _SplitterInfoView.SafeMemoryMappedViewHandle.ReleasePointer();
                    _SplitterInfoPtr = null;
                }

                _SplitterInfoView.Dispose();
            }

        }

    }
}