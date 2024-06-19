using MonoMod;
using System;

namespace Celeste {
    static class patch_RunLengthEncoding {

        [MonoModReplace]
        public static string Decode(byte[] bytes) => Decode(bytes.AsSpan());
        
        // Optimise this method using spans and a shared buffer
        public static string Decode(ReadOnlySpan<byte> bytes) {
            _decodeBuffer ??= new char[4098];
            
            int written = 0;
            var decodeBuffer = _decodeBuffer.AsSpan();
            for (int index = 0; index < bytes.Length - 1; index += 2) {
                byte count = bytes[index];
                char c = (char) bytes[index + 1];
                int endIndex = written + count;

                // resize our buffer if needed
                if (decodeBuffer.Length <= endIndex) {
                    Array.Resize(ref _decodeBuffer, Math.Max(_decodeBuffer.Length * 2, endIndex));
                    decodeBuffer = _decodeBuffer.AsSpan();
                }
                
                decodeBuffer[written..endIndex].Fill(c);
                written = endIndex;
            }
            
            return decodeBuffer[..written].ToString();
        }

        /// <summary>
        /// A buffer used by <see cref="Decode(ReadOnlySpan{byte})"/> to create the returned string.
        /// </summary>
        [ThreadStatic]
        private static char[] _decodeBuffer;
    }
}
