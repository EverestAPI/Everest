// From https://github.com/differentrain/YYProject.XXHash

/*
MIT License

Copyright (c) 2018 differentrain

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;


namespace YYProject.XXHash {
    // see details: https://github.com/Cyan4973/xxHash/blob/dev/doc/xxhash_spec.md

    /// <summary>
    /// Represents the class which provides a implementation of the xxHash32 algorithm.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>   
    public sealed class XXHash32 : HashAlgorithm {
        private const uint PRIME32_1 = 2654435761U;
        private const uint PRIME32_2 = 2246822519U;
        private const uint PRIME32_3 = 3266489917U;
        private const uint PRIME32_4 = 668265263U;
        private const uint PRIME32_5 = 374761393U;

        private static readonly Func<byte[], int, uint> FuncGetLittleEndianUInt32;
        private static readonly Func<uint, uint> FuncGetFinalHashUInt32;

        private uint _Seed32;

        private uint _ACC32_1;
        private uint _ACC32_2;
        private uint _ACC32_3;
        private uint _ACC32_4;

        private uint _Hash32;


        private int _RemainingLength;
        private long _TotalLength = 0;
        private int _CurrentIndex;
        private byte[] _CurrentArray;

        static XXHash32() {
            if (BitConverter.IsLittleEndian) {

                FuncGetLittleEndianUInt32 = new Func<byte[], int, uint>((x, i) => {
                    unsafe {
                        fixed (byte* array = x) {
                            return *(uint*) (array + i);
                        }
                    }
                });
                FuncGetFinalHashUInt32 = new Func<uint, uint>(i => (i & 0x000000FFU) << 24 | (i & 0x0000FF00U) << 8 | (i & 0x00FF0000U) >> 8 | (i & 0xFF000000U) >> 24);
            } else {
                FuncGetLittleEndianUInt32 = new Func<byte[], int, uint>((x, i) => {
                    unsafe {
                        fixed (byte* array = x) {
                            return (uint) (array[i++] | (array[i++] << 8) | (array[i++] << 16) | (array[i] << 24));
                        }
                    }
                });
                FuncGetFinalHashUInt32 = new Func<uint, uint>(i => i);
            }
        }

        /// <summary>
        /// Creates an instance of <see cref="XXHash32"/> class by default seed(0).
        /// </summary>
        /// <returns></returns>
        public new static XXHash32 Create() => new XXHash32();

        /// <summary>
        /// Creates an instance of the specified implementation of XXHash32 algorithm.
        /// <para>This method always throws <see cref="NotSupportedException"/>. </para>
        /// </summary>
        /// <param name="algName">The hash algorithm implementation to use.</param>
        /// <returns>This method always throws <see cref="NotSupportedException"/>. </returns>
        /// <exception cref="NotSupportedException">This method is not be supported.</exception>
        public new static XXHash32 Create(string algName) => throw new NotSupportedException("This method is not be supported.");

        /// <summary>
        /// Initializes a new instance of the <see cref="XXHash32"/> class by default seed(0).
        /// </summary>
        public XXHash32() => Initialize(0);

        /// <summary>
        /// Initializes a new instance of the <see cref="XXHash32"/> class, and sets the <see cref="Seed"/> to the specified value.
        /// </summary>
        /// <param name="seed">Represent the seed to be used for xxHash32 computing.</param>
        public XXHash32(uint seed) => Initialize(seed);

        /// <summary>
        /// Gets the <see cref="uint"/> value of the computed hash code.
        /// </summary>
        /// <exception cref="InvalidOperationException">Hash computation has not yet completed.</exception>
        public uint HashUInt32 => State == 0 ? _Hash32 : throw new InvalidOperationException("Hash computation has not yet completed.");

        /// <summary>
        /// Gets or sets the value of seed used by xxHash32 algorithm.
        /// </summary>
        /// <exception cref="InvalidOperationException">Hash computation has not yet completed.</exception>
        public uint Seed {
            get => _Seed32;
            set {

                if (value != _Seed32) {
                    if (State != 0)
                        throw new InvalidOperationException("Hash computation has not yet completed.");
                    _Seed32 = value;
                    Initialize();
                }
            }
        }


        /// <summary>
        /// Initializes this instance for new hash computing.
        /// </summary>
        public override void Initialize() {
            _ACC32_1 = _Seed32 + PRIME32_1 + PRIME32_2;
            _ACC32_2 = _Seed32 + PRIME32_2;
            _ACC32_3 = _Seed32 + 0;
            _ACC32_4 = _Seed32 - PRIME32_1;
        }



        /// <summary>
        /// Routes data written to the object into the hash algorithm for computing the hash.
        /// </summary>
        /// <param name="array">The input to compute the hash code for.</param>
        /// <param name="ibStart">The offset into the byte array from which to begin using data.</param>
        /// <param name="cbSize">The number of bytes in the byte array to use as data.</param>
        protected override void HashCore(byte[] array, int ibStart, int cbSize) {
            if (State != 1)
                State = 1;
            var size = cbSize - ibStart;
            _RemainingLength = size & 15;
            if (cbSize >= 16) {
                var limit = size - _RemainingLength;
                do {
                    _ACC32_1 = Round32(_ACC32_1, FuncGetLittleEndianUInt32(array, ibStart));
                    ibStart += 4;
                    _ACC32_2 = Round32(_ACC32_2, FuncGetLittleEndianUInt32(array, ibStart));
                    ibStart += 4;
                    _ACC32_3 = Round32(_ACC32_3, FuncGetLittleEndianUInt32(array, ibStart));
                    ibStart += 4;
                    _ACC32_4 = Round32(_ACC32_4, FuncGetLittleEndianUInt32(array, ibStart));
                    ibStart += 4;
                } while (ibStart < limit);
            }
            _TotalLength += cbSize;

            if (_RemainingLength != 0) {
                _CurrentArray = array;
                _CurrentIndex = ibStart;
            }
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is processed by the cryptographic stream object.
        /// </summary>
        /// <returns>The computed hash code.</returns>
        protected override byte[] HashFinal() {
            if (_TotalLength >= 16) {
                _Hash32 = RotateLeft32_1(_ACC32_1) + RotateLeft32_7(_ACC32_2) + RotateLeft32_12(_ACC32_3) + RotateLeft32_18(_ACC32_4);
            } else {
                _Hash32 = _Seed32 + PRIME32_5;
            }

            _Hash32 += (uint) _TotalLength;

            while (_RemainingLength >= 4) {
                _Hash32 = RotateLeft32_17(_Hash32 + FuncGetLittleEndianUInt32(_CurrentArray, _CurrentIndex) * PRIME32_3) * PRIME32_4;
                _CurrentIndex += 4;
                _RemainingLength -= 4;
            }
            unsafe {
                fixed (byte* arrayPtr = _CurrentArray) {
                    while (_RemainingLength-- >= 1) {
                        _Hash32 = RotateLeft32_11(_Hash32 + arrayPtr[_CurrentIndex++] * PRIME32_5) * PRIME32_1;
                    }
                }
            }
            _Hash32 = (_Hash32 ^ (_Hash32 >> 15)) * PRIME32_2;
            _Hash32 = (_Hash32 ^ (_Hash32 >> 13)) * PRIME32_3;
            _Hash32 ^= _Hash32 >> 16;

            _TotalLength = State = 0;

            return BitConverter.GetBytes(FuncGetFinalHashUInt32(_Hash32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Round32(uint input, uint value) => RotateLeft32_13(input + (value * PRIME32_2)) * PRIME32_1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft32_1(uint value) => (value << 1) | (value >> 31); // _ACC32_1
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft32_7(uint value) => (value << 7) | (value >> 25); // _ACC32_2
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft32_11(uint value) => (value << 11) | (value >> 21);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft32_12(uint value) => (value << 12) | (value >> 20);// _ACC32_3
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft32_13(uint value) => (value << 13) | (value >> 19);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft32_17(uint value) => (value << 17) | (value >> 15);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft32_18(uint value) => (value << 18) | (value >> 14); // _ACC32_4

        private void Initialize(uint seed) {
            HashSizeValue = 32;
            _Seed32 = seed;
            Initialize();
        }

    }

    /// <summary>
    /// Represents the class which provides a implementation of the xxHash64 algorithm.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public sealed class XXHash64 : HashAlgorithm {
        private const ulong PRIME64_1 = 11400714785074694791UL;
        private const ulong PRIME64_2 = 14029467366897019727UL;
        private const ulong PRIME64_3 = 1609587929392839161UL;
        private const ulong PRIME64_4 = 9650029242287828579UL;
        private const ulong PRIME64_5 = 2870177450012600261UL;

        private static readonly Func<byte[], int, uint> FuncGetLittleEndianUInt32;
        private static readonly Func<byte[], int, ulong> FuncGetLittleEndianUInt64;
        private static readonly Func<ulong, ulong> FuncGetFinalHashUInt64;

        private ulong _Seed64;

        private ulong _ACC64_1;
        private ulong _ACC64_2;
        private ulong _ACC64_3;
        private ulong _ACC64_4;
        private ulong _Hash64;

        private int _RemainingLength;
        private long _TotalLength;
        private int _CurrentIndex;
        private byte[] _CurrentArray;



        static XXHash64() {
            if (BitConverter.IsLittleEndian) {
                FuncGetLittleEndianUInt32 = new Func<byte[], int, uint>((x, i) => {
                    unsafe {
                        fixed (byte* array = x) {
                            return *(uint*) (array + i);
                        }
                    }
                });
                FuncGetLittleEndianUInt64 = new Func<byte[], int, ulong>((x, i) => {
                    unsafe {
                        fixed (byte* array = x) {
                            return *(ulong*) (array + i);
                        }
                    }
                });
                FuncGetFinalHashUInt64 = new Func<ulong, ulong>(i => (i & 0x00000000000000FFUL) << 56 | (i & 0x000000000000FF00UL) << 40 | (i & 0x0000000000FF0000UL) << 24 | (i & 0x00000000FF000000UL) << 8 | (i & 0x000000FF00000000UL) >> 8 | (i & 0x0000FF0000000000UL) >> 24 | (i & 0x00FF000000000000UL) >> 40 | (i & 0xFF00000000000000UL) >> 56);
            } else {
                FuncGetLittleEndianUInt32 = new Func<byte[], int, uint>((x, i) => {
                    unsafe {
                        fixed (byte* array = x) {
                            return (uint) (array[i++] | (array[i++] << 8) | (array[i++] << 16) | (array[i] << 24));
                        }
                    }
                });
                FuncGetLittleEndianUInt64 = new Func<byte[], int, ulong>((x, i) => {
                    unsafe {
                        fixed (byte* array = x) {
                            return array[i++] | ((ulong) array[i++] << 8) | ((ulong) array[i++] << 16) | ((ulong) array[i++] << 24) | ((ulong) array[i++] << 32) | ((ulong) array[i++] << 40) | ((ulong) array[i++] << 48) | ((ulong) array[i] << 56);
                        }
                    }
                });
                FuncGetFinalHashUInt64 = new Func<ulong, ulong>(i => i);
            }
        }

        /// <summary>
        /// Creates an instance of <see cref="XXHash64"/> class by default seed(0).
        /// </summary>
        /// <returns></returns>
        public new static XXHash64 Create() => new XXHash64();

        /// <summary>
        /// Creates an instance of the specified implementation of XXHash64 algorithm.
        /// <para>This method always throws <see cref="NotSupportedException"/>. </para>
        /// </summary>
        /// <param name="algName">The hash algorithm implementation to use.</param>
        /// <returns>This method always throws <see cref="NotSupportedException"/>. </returns>
        /// <exception cref="NotSupportedException">This method is not be supported.</exception>
        public new static XXHash64 Create(string algName) => throw new NotSupportedException("This method is not be supported.");

        /// <summary>
        /// Initializes a new instance of the <see cref="XXHash64"/> class by default seed(0).
        /// </summary>
        public XXHash64() => Initialize(0);


        /// <summary>
        /// Initializes a new instance of the <see cref="XXHash64"/> class, and sets the <see cref="Seed"/> to the specified value.
        /// </summary>
        /// <param name="seed">Represent the seed to be used for xxHash64 computing.</param>
        public XXHash64(uint seed) => Initialize(seed);


        /// <summary>
        /// Gets the <see cref="ulong"/> value of the computed hash code.
        /// </summary>
        /// <exception cref="InvalidOperationException">Computation has not yet completed.</exception>
        public ulong HashUInt64 => State == 0 ? _Hash64 : throw new InvalidOperationException("Computation has not yet completed.");

        /// <summary>
        ///  Gets or sets the value of seed used by xxHash64 algorithm.
        /// </summary>
        /// <exception cref="InvalidOperationException">Computation has not yet completed.</exception>
        public ulong Seed {
            get => _Seed64;
            set {
                if (value != _Seed64) {
                    if (State != 0)
                        throw new InvalidOperationException("Computation has not yet completed.");
                    _Seed64 = value;
                    Initialize();
                }
            }
        }


        /// <summary>
        /// Initializes this instance for new hash computing.
        /// </summary>
        public override void Initialize() {
            _ACC64_1 = _Seed64 + PRIME64_1 + PRIME64_2;
            _ACC64_2 = _Seed64 + PRIME64_2;
            _ACC64_3 = _Seed64 + 0;
            _ACC64_4 = _Seed64 - PRIME64_1;
        }

        /// <summary>
        /// Routes data written to the object into the hash algorithm for computing the hash.
        /// </summary>
        /// <param name="array">The input to compute the hash code for.</param>
        /// <param name="ibStart">The offset into the byte array from which to begin using data.</param>
        /// <param name="cbSize">The number of bytes in the byte array to use as data.</param>
        protected override void HashCore(byte[] array, int ibStart, int cbSize) {
            if (State != 1)
                State = 1;
            var size = cbSize - ibStart;
            _RemainingLength = size & 31;
            if (cbSize >= 32) {
                var limit = size - _RemainingLength;
                do {
                    _ACC64_1 = Round64(_ACC64_1, FuncGetLittleEndianUInt64(array, ibStart));
                    ibStart += 8;
                    _ACC64_2 = Round64(_ACC64_2, FuncGetLittleEndianUInt64(array, ibStart));
                    ibStart += 8;
                    _ACC64_3 = Round64(_ACC64_3, FuncGetLittleEndianUInt64(array, ibStart));
                    ibStart += 8;
                    _ACC64_4 = Round64(_ACC64_4, FuncGetLittleEndianUInt64(array, ibStart));
                    ibStart += 8;
                } while (ibStart < limit);
            }
            _TotalLength += cbSize;
            if (_RemainingLength != 0) {
                _CurrentArray = array;
                _CurrentIndex = ibStart;
            }
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is processed by the cryptographic stream object.
        /// </summary>
        /// <returns>The computed hash code.</returns>
        protected override byte[] HashFinal() {
            if (_TotalLength >= 32) {
                _Hash64 = RotateLeft64_1(_ACC64_1) + RotateLeft64_7(_ACC64_2) + RotateLeft64_12(_ACC64_3) + RotateLeft64_18(_ACC64_4);
                _Hash64 = MergeRound64(_Hash64, _ACC64_1);
                _Hash64 = MergeRound64(_Hash64, _ACC64_2);
                _Hash64 = MergeRound64(_Hash64, _ACC64_3);
                _Hash64 = MergeRound64(_Hash64, _ACC64_4);
            } else {
                _Hash64 = _Seed64 + PRIME64_5;
            }

            _Hash64 += (ulong) _TotalLength;

            while (_RemainingLength >= 8) {
                _Hash64 = RotateLeft64_27(_Hash64 ^ Round64(0, FuncGetLittleEndianUInt64(_CurrentArray, _CurrentIndex))) * PRIME64_1 + PRIME64_4;
                _CurrentIndex += 8;
                _RemainingLength -= 8;
            }

            while (_RemainingLength >= 4) {
                _Hash64 = RotateLeft64_23(_Hash64 ^ (FuncGetLittleEndianUInt32(_CurrentArray, _CurrentIndex) * PRIME64_1)) * PRIME64_2 + PRIME64_3;
                _CurrentIndex += 4;
                _RemainingLength -= 4;
            }

            unsafe {
                fixed (byte* arrayPtr = _CurrentArray) {
                    while (_RemainingLength-- >= 1) {
                        _Hash64 = RotateLeft64_11(_Hash64 ^ (arrayPtr[_CurrentIndex++] * PRIME64_5)) * PRIME64_1;
                    }
                }
            }

            _Hash64 = (_Hash64 ^ (_Hash64 >> 33)) * PRIME64_2;
            _Hash64 = (_Hash64 ^ (_Hash64 >> 29)) * PRIME64_3;
            _Hash64 ^= _Hash64 >> 32;

            _TotalLength = State = 0;
            return BitConverter.GetBytes(FuncGetFinalHashUInt64(_Hash64));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MergeRound64(ulong input, ulong value) => (input ^ Round64(0, value)) * PRIME64_1 + PRIME64_4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Round64(ulong input, ulong value) => RotateLeft64_31(input + (value * PRIME64_2)) * PRIME64_1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft64_1(ulong value) => (value << 1) | (value >> 63); // _ACC64_1
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft64_7(ulong value) => (value << 7) | (value >> 57); //  _ACC64_2
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft64_11(ulong value) => (value << 11) | (value >> 53);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft64_12(ulong value) => (value << 12) | (value >> 52);// _ACC64_3
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft64_18(ulong value) => (value << 18) | (value >> 46); // _ACC64_4
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft64_23(ulong value) => (value << 23) | (value >> 41);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft64_27(ulong value) => (value << 27) | (value >> 37);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft64_31(ulong value) => (value << 31) | (value >> 33);

        private void Initialize(ulong seed) {
            HashSizeValue = 64;
            _Seed64 = seed;
            Initialize();
        }

    }
}

