#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Runtime.InteropServices;
using System.Text;
using MonoMod;

namespace FMOD.Studio {
    class patch_EventInstance : EventInstance {
        public patch_EventInstance(IntPtr raw) : base(raw) {
        }

        [DllImport("fmodstudio", EntryPoint = "FMOD_Studio_EventInstance_SetParameterByName")]
        private static extern RESULT FMOD_Studio_EventInstance_SetParameterByName(IntPtr instance, byte[] name, float value, bool ignoreseekspeed);

        [DllImport("fmodstudio", EntryPoint = "FMOD_Studio_EventInstance_GetParameterByName")]
        private static extern RESULT FMOD_Studio_EventInstance_GetParameterByName(IntPtr instance, byte[] name, out float value, out float finalvalue);

        [MonoModReplace]
        public new RESULT setParameterValue(string name, float value) {
            if (rawPtr == IntPtr.Zero)
                return RESULT.ERR_INVALID_HANDLE;

            byte[] bytes = Encoding.UTF8.GetBytes(name + "\0");
            return FMOD_Studio_EventInstance_SetParameterByName(rawPtr, bytes, value, false);
        }

        [MonoModReplace]
        public new RESULT getParameterValue(string name, out float value, out float finalvalue) {
            if (rawPtr == IntPtr.Zero) {
                value = 0f;
                finalvalue = 0f;
                return RESULT.ERR_INVALID_HANDLE;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(name + "\0");
            return FMOD_Studio_EventInstance_GetParameterByName(rawPtr, bytes, out value, out finalvalue);
        }
    }
}
