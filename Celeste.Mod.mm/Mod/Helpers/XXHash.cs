
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Celeste.Mod {
    public sealed class XXHash64 : HashAlgorithm {
        private const ulong PRIME1 = 0x9e3779b185ebca87ul, PRIME2 = 0xc2b2ae3d27d4eb4ful, PRIME3 = 0x165667b19e3779f9ul, PRIME4 = 0x85ebca77c2b2ae63ul, PRIME5 = 0x27d4eb2f165667c5ul;

        public static new XXHash64 Create() => new XXHash64();
        public static new XXHash64 Create(string name) => throw new NotSupportedException();

        private ulong acc1, acc2, acc3, acc4;
        private ulong inLength;

        private byte[] stripeBuf = new byte[32];
        private int stripeFillSize = 0;

        public XXHash64() {
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("Big-Endian platforms not supported!");
            Initialize();
        }

        public override void Initialize() {
            unchecked {
                acc1 = PRIME1 + PRIME2;
                acc2 = PRIME2;
                acc3 = 0;
                acc4 = (ulong) -(long) PRIME1;
                inLength = 0;
            }
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize) {
            inLength += (ulong) cbSize;

            // Complete the previous stripe
            if (stripeFillSize > 0) {
                int laneAddSize = cbSize < (32 - stripeFillSize) ? cbSize : (32 - stripeFillSize);
                Buffer.BlockCopy(array, ibStart, stripeBuf, stripeFillSize, laneAddSize);
                if ((stripeFillSize += laneAddSize) < 32)
                    return;

                ProcessStripe(stripeBuf, 0);
                ibStart += laneAddSize;
                cbSize -= stripeFillSize;
            }

            // Process strips while we have enough data
            for (; cbSize >= 32; ibStart += 32, cbSize -= 32)
                ProcessStripe(array, ibStart);

            // Add remaining data to the lane buffer
            Buffer.BlockCopy(array, ibStart, stripeBuf, 0, cbSize);
            stripeFillSize = cbSize;
        }

        private void ProcessStripe(byte[] buf, int off) {
            unchecked {
                static ulong rot31(ulong v) => (v << 31) | (v >> (64-31));
                acc1 = rot31(acc1 + Unsafe.ReadUnaligned<ulong>(ref buf[off + 00]) * PRIME2) * PRIME1;
                acc2 = rot31(acc2 + Unsafe.ReadUnaligned<ulong>(ref buf[off + 08]) * PRIME2) * PRIME1;
                acc3 = rot31(acc3 + Unsafe.ReadUnaligned<ulong>(ref buf[off + 16]) * PRIME2) * PRIME1;
                acc4 = rot31(acc4 + Unsafe.ReadUnaligned<ulong>(ref buf[off + 24]) * PRIME2) * PRIME1;
            }
        }

        protected override byte[] HashFinal() {
            unchecked {
                static ulong rot01(ulong v) => (v << 01) | (v >> (64-01));
                static ulong rot07(ulong v) => (v << 07) | (v >> (64-07));
                static ulong rot12(ulong v) => (v << 12) | (v >> (64-12));
                static ulong rot18(ulong v) => (v << 18) | (v >> (64-18));
                static ulong rot31(ulong v) => (v << 31) | (v >> (64-31));

                static ulong round(ulong a, ulong l) => rot31(a + l * PRIME2) * PRIME1;
                static ulong merge(ulong acc, ulong accN) => (acc ^ round(0, accN))*PRIME1 + PRIME4;

                // Accumulator convergence
                ulong acc = rot01(acc1) + rot07(acc2) + rot12(acc3) + rot18(acc4);
                acc = merge(acc, acc1);
                acc = merge(acc, acc2);
                acc = merge(acc, acc3);
                acc = merge(acc, acc4);

                // Add input length
                acc += inLength;

                // Consume remaining input
                int stripeOff = 0;
                for (; stripeFillSize >= 8; stripeOff += 8, stripeFillSize -= 8) {
                    acc ^= round(0, Unsafe.ReadUnaligned<ulong>(ref stripeBuf[stripeOff]));
                    acc = ((acc << 27) | (acc >> (64-27)))*PRIME1 + PRIME4;
                }
                for (; stripeFillSize >= 4; stripeOff += 4, stripeFillSize -= 4) {
                    acc ^= Unsafe.ReadUnaligned<uint>(ref stripeBuf[stripeOff]) * PRIME1;
                    acc = ((acc << 23) | (acc >> (64-23)))*PRIME2 + PRIME3;
                }
                for (; stripeFillSize >= 1; stripeOff += 1, stripeFillSize -= 1) {
                    acc ^= Unsafe.ReadUnaligned<byte>(ref stripeBuf[stripeOff]) * PRIME5;
                    acc = ((acc << 11) | (acc >> (64-11)))*PRIME1;
                }

                // Final mix
                acc ^= acc >> 33;
                acc *= PRIME2;
                acc ^= acc >> 29;
                acc *= PRIME3;
                acc ^= acc >> 32;

                return new byte[] {
                    (byte) (acc >> 56),
                    (byte) (acc >> 48),
                    (byte) (acc >> 40),
                    (byte) (acc >> 32),
                    (byte) (acc >> 24),
                    (byte) (acc >> 16),
                    (byte) (acc >> 08),
                    (byte) (acc >> 00)
                };
            }
        }

    }
}