using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.Helpers {
    public sealed class ZipAssetStream : Stream {

        // I'm overcomplicating this. -ade

        private const int DummySize = 4096;
        private static readonly byte[] Dummy = new byte[DummySize];

        private bool FullyBuffered = false;
        private Stream Inner;
        private ZipEntry FakeEntry;

        private readonly ZipModAsset Asset;
        private readonly ZipModContent.ZipModSecret Secret;

        public ZipAssetStream(ZipModAsset asset, ZipModContent.ZipModSecret secret) {
            Asset = asset;
            Secret = secret;
            FakeEntry = secret.OpenParaEntry(asset.Entry);
            Inner = FakeEntry.OpenReader();
        }

        public override bool CanRead => Inner.CanRead;

        // FNA's texture parser will still seek even though this is false...
        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => Inner.Length;

        public override long Position {
            get => Inner.Position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush() {
            Inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return Inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) {
            if (FullyBuffered)
                return Inner.Seek(offset, origin);

            if (origin == SeekOrigin.Begin) {
                offset -= Inner.Position;
                origin = SeekOrigin.Current;
            }

            if (offset < 0 || origin == SeekOrigin.End) {
                // Welp. FMOD loves to seek backwards.
                // While we could go full low level indexing and / or improve buffering, this will do.
                // ... and it was already the default in the past anyway~
                FullyBuffered = true;
                if (origin == SeekOrigin.Current) {
                    offset += Inner.Position;
                    origin = SeekOrigin.Begin;
                }
                Stream full = FakeEntry.ExtractStream();
                Close(); // We no longer need FakeEntry.
                Inner = full;
                return Inner.Seek(offset, origin);
            }

            if (offset == 0)
                return Position;

            for (; offset > DummySize; offset -= DummySize)
                Inner.Read(Dummy, 0, DummySize);
            Inner.Read(Dummy, 0, (int) offset);
            return Position;
        }

        public override void SetLength(long value) {
            Inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            Inner.Write(buffer, offset, count);
        }

        public override IAsyncResult BeginRead(byte[] array, int offset, int count, AsyncCallback callback, object state) {
            return Inner.BeginRead(array, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult) {
            return Inner.EndRead(asyncResult);
        }

        public override int ReadByte() {
            return Inner.ReadByte();
        }

        public override IAsyncResult BeginWrite(byte[] array, int offset, int count, AsyncCallback callback, object state) {
            return Inner.BeginWrite(array, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult) {
            Inner.EndWrite(asyncResult);
        }

        public override void WriteByte(byte value) {
            Inner.WriteByte(value);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            return Inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            return Inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override Task FlushAsync(CancellationToken cancellationToken) {
            return Inner.FlushAsync(cancellationToken);
        }

        public override void Close() {
            Inner.Close();
            if (FakeEntry != null) {
                Secret.CloseParaEntry(FakeEntry);
                FakeEntry = null;
            }
        }

        protected override void Dispose(bool disposing) {
            Inner.Dispose();
            if (FakeEntry != null) {
                Secret.CloseParaEntry(FakeEntry);
                FakeEntry = null;
            }
        }

    }
}
