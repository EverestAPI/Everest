using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Celeste.Mod.Helpers {
    /// <summary>
    /// A stream giving access to a ZipArchiveEntry and putting a lock on the ZipArchive when calling any method.
    /// This makes reading from multiple entries at the same time thread-safe (kind of) (I think).
    /// It also implements Seek, Position and Length.
    /// </summary>
    public class SynchronizedZipEntryStream : Stream {
        private readonly ZipArchive archive;
        private readonly ZipArchiveEntry entry;
        private Stream wrappedStream;
        private long position;

        public SynchronizedZipEntryStream(ZipArchiveEntry entry) {
            this.entry = entry;

            archive = entry.Archive;
            Length = entry.Length;

            lock (archive) {
                // open the stream
                reset();
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }

        // reads with locking, and updates Position
        public override int Read(byte[] buffer, int offset, int count) {
            lock (archive) {
                int readCount = readAsMuchAsPossible(buffer, offset, count);
                position += readCount;
                return readCount;
            }
        }

        // closes and reopens the ZipArchiveEntry in order to rewind the stream
        private void reset() {
            wrappedStream?.Close();
            wrappedStream = new BufferedStream(entry.Open());
            position = 0;
        }

        // closes the stream of the ZipArchiveEntry
        protected override void Dispose(bool disposing) {
            lock (archive) {
                wrappedStream.Dispose();
            }
        }

        public override long Position {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        }

        // DIY seeking
        public override long Seek(long offset, SeekOrigin origin) {
            lock (archive) {
                // target = position from the beginning of the file
                long target = offset;
                if (origin == SeekOrigin.Current) target += Position;
                if (origin == SeekOrigin.End) target += Length;

                if (target < Position) {
                    // seek back => go back to the beginning
                    reset();
                }

                if (target > Position) {
                    // seek forward by just skipping the required amount of bytes
                    // (this also handles seeking back, by skipping from 0 to the requested position)
                    skip((int) (target - Position));
                }

                if (target != Position) {
                    Logger.Warn("SynchronizedZipEntryStream", $"[{entry.FullName}] Couldn't seek to position {target}! Sought to {Position}/{Length}.");
                }

                return Position;
            }
        }

        // read repeatedly until we read the requested amount of bytes, or we reached the end of the stream
        // (the underlying stream returns 0).
        // we *should* be able to return fewer bytes than requested, but FNA3D *really* doesn't like that.
        private int readAsMuchAsPossible(byte[] buffer, int offset, int count) {
            int remainingCount = count;
            while (remainingCount > 0) {
                int read = wrappedStream.Read(buffer, offset, remainingCount);
                if (read == 0) break;
                offset += read;
                remainingCount -= read;
            }
            return count - remainingCount;
        }

        private const int trashbinSize = 8192;
        private static byte[] trashbin = new byte[trashbinSize];

        // "skips" bytes by reading them and blatantly ignoring them
        private void skip(long count) {
            while (count > 0) {
                int read = wrappedStream.Read(trashbin, 0, count > trashbinSize ? trashbinSize : (int) count);
                if (read == 0) return;
                position += read;
                count -= read;
            }
        }

        // those are useless for a read-only stream

        public override void Flush() {
            throw new NotSupportedException("Write not supported!");
        }
        public override void SetLength(long value) {
            throw new NotSupportedException("Write not supported!");
        }
        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException("Write not supported!");
        }
    }
}
