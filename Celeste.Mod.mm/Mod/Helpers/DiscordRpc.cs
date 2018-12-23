#pragma warning disable CS0649 // private readonly fields filled via reflection

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using MonoMod.Utils;

namespace Celeste.Mod.Helpers {
    // Based on https://github.com/discordapp/discord-rpc/blob/master/examples/button-clicker/Assets/DiscordRpc.cs
    public class DiscordRpc {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ReadyCallback(ref DiscordUser connectedUser);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DisconnectedCallback(int errorCode, string message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ErrorCallback(int errorCode, string message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void JoinCallback(string secret);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SpectateCallback(string secret);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void RequestCallback(ref DiscordUser request);

        public struct EventHandlers {
            public ReadyCallback readyCallback;
            public DisconnectedCallback disconnectedCallback;
            public ErrorCallback errorCallback;
            public JoinCallback joinCallback;
            public SpectateCallback spectateCallback;
            public RequestCallback requestCallback;
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct RichPresenceStruct {
            public IntPtr state; /* max 128 bytes */
            public IntPtr details; /* max 128 bytes */
            public long startTimestamp;
            public long endTimestamp;
            public IntPtr largeImageKey; /* max 32 bytes */
            public IntPtr largeImageText; /* max 128 bytes */
            public IntPtr smallImageKey; /* max 32 bytes */
            public IntPtr smallImageText; /* max 128 bytes */
            public IntPtr partyId; /* max 128 bytes */
            public int partySize;
            public int partyMax;
            public IntPtr matchSecret; /* max 128 bytes */
            public IntPtr joinSecret; /* max 128 bytes */
            public IntPtr spectateSecret; /* max 128 bytes */
            public bool instance;
        }

        [Serializable]
        public struct DiscordUser {
            public string userId;
            public string username;
            public string discriminator;
            public string avatar;
        }

        public enum Reply {
            No = 0,
            Yes = 1,
            Ignore = 2
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Discord_Initialize(string applicationId, ref EventHandlers handlers, bool autoRegister, string optionalSteamId);
        [DynDllImport("discord-rpc", "Discord_Initialize")]
        public readonly static Discord_Initialize Initialize;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Discord_Shutdown();
        [DynDllImport("discord-rpc", "Discord_Shutdown")]
        public readonly static Discord_Shutdown Shutdown;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Discord_RunCallbacks();
        [DynDllImport("discord-rpc", "Discord_RunCallbacks")]
        public readonly static Discord_RunCallbacks RunCallbacks;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Discord_UpdatePresence(ref RichPresenceStruct presence);
        [DynDllImport("discord-rpc", "Discord_UpdatePresence")]
        private readonly static Discord_UpdatePresence UpdatePresenceNative;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Discord_ClearPresence();
        [DynDllImport("discord-rpc", "Discord_ClearPresence")]
        public readonly static Discord_ClearPresence ClearPresence;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Discord_Respond(string userId, Reply reply);
        [DynDllImport("discord-rpc", "Discord_Respond")]
        public readonly static Discord_Respond Respond;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Discord_UpdateHandlers(ref EventHandlers handlers);
        [DynDllImport("discord-rpc", "Discord_UpdateHandlers")]
        public readonly static Discord_UpdateHandlers UpdateHandlers;

        public static void UpdatePresence(RichPresence presence) {
            var presencestruct = presence.GetStruct();
            UpdatePresenceNative(ref presencestruct);
            presence.FreeMem();
        }

        public class RichPresence {
            private RichPresenceStruct _presence;
            private readonly List<IntPtr> _buffers = new List<IntPtr>(10);

            public string state; /* max 128 bytes */
            public string details; /* max 128 bytes */
            public long startTimestamp;
            public long endTimestamp;
            public string largeImageKey; /* max 32 bytes */
            public string largeImageText; /* max 128 bytes */
            public string smallImageKey; /* max 32 bytes */
            public string smallImageText; /* max 128 bytes */
            public string partyId; /* max 128 bytes */
            public int partySize;
            public int partyMax;
            public string matchSecret; /* max 128 bytes */
            public string joinSecret; /* max 128 bytes */
            public string spectateSecret; /* max 128 bytes */
            public bool instance;

            /// <summary>
            /// Get the <see cref="RichPresenceStruct"/> reprensentation of this instance
            /// </summary>
            /// <returns><see cref="RichPresenceStruct"/> reprensentation of this instance</returns>
            internal RichPresenceStruct GetStruct() {
                if (_buffers.Count > 0) {
                    FreeMem();
                }

                _presence.state = StrToPtr(state, 128);
                _presence.details = StrToPtr(details, 128);
                _presence.startTimestamp = startTimestamp;
                _presence.endTimestamp = endTimestamp;
                _presence.largeImageKey = StrToPtr(largeImageKey, 32);
                _presence.largeImageText = StrToPtr(largeImageText, 128);
                _presence.smallImageKey = StrToPtr(smallImageKey, 32);
                _presence.smallImageText = StrToPtr(smallImageText, 128);
                _presence.partyId = StrToPtr(partyId, 128);
                _presence.partySize = partySize;
                _presence.partyMax = partyMax;
                _presence.matchSecret = StrToPtr(matchSecret, 128);
                _presence.joinSecret = StrToPtr(joinSecret, 128);
                _presence.spectateSecret = StrToPtr(spectateSecret, 128);
                _presence.instance = instance;

                return _presence;
            }

            /// <summary>
            /// Returns a pointer to a representation of the given string with a size of maxbytes
            /// </summary>
            /// <param name="input">String to convert</param>
            /// <param name="maxbytes">Max number of bytes to use</param>
            /// <returns>Pointer to the UTF-8 representation of input</returns>
            private IntPtr StrToPtr(string input, int maxbytes) {
                if (string.IsNullOrEmpty(input)) return IntPtr.Zero;
                var convstr = StrClampBytes(input, maxbytes);
                var convbytecnt = Encoding.UTF8.GetByteCount(convstr);
                var buffer = Marshal.AllocHGlobal(convbytecnt);
                _buffers.Add(buffer);
                Marshal.Copy(Encoding.UTF8.GetBytes(convstr), 0, buffer, convbytecnt);
                return buffer;
            }

            /// <summary>
            /// Convert string to UTF-8 and add null termination
            /// </summary>
            /// <param name="toconv">string to convert</param>
            /// <returns>UTF-8 representation of toconv with added null termination</returns>
            private static string StrToUtf8NullTerm(string toconv) {
                var str = toconv.Trim();
                var bytes = Encoding.Default.GetBytes(str);
                if (bytes.Length > 0 && bytes[bytes.Length - 1] != 0) {
                    str += "\0\0";
                }
                return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(str));
            }

            /// <summary>
            /// Clamp the string to the given byte length preserving null termination
            /// </summary>
            /// <param name="toclamp">string to clamp</param>
            /// <param name="maxbytes">max bytes the resulting string should have (including null termination)</param>
            /// <returns>null terminated string with a byte length less or equal to maxbytes</returns>
            private static string StrClampBytes(string toclamp, int maxbytes) {
                var str = StrToUtf8NullTerm(toclamp);
                var strbytes = Encoding.UTF8.GetBytes(str);

                if (strbytes.Length <= maxbytes) {
                    return str;
                }

                var newstrbytes = new byte[] { };
                Array.Copy(strbytes, 0, newstrbytes, 0, maxbytes - 1);
                newstrbytes[newstrbytes.Length - 1] = 0;
                newstrbytes[newstrbytes.Length - 2] = 0;

                return Encoding.UTF8.GetString(newstrbytes);
            }

            /// <summary>
            /// Free the allocated memory for conversion to <see cref="RichPresenceStruct"/>
            /// </summary>
            internal void FreeMem() {
                for (var i = _buffers.Count - 1; i >= 0; i--) {
                    Marshal.FreeHGlobal(_buffers[i]);
                    _buffers.RemoveAt(i);
                }
            }
        }
    }
}