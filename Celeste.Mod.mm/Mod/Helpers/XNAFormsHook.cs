using System;
using System.Runtime.InteropServices;

namespace Celeste.Mod.Helpers {
    internal sealed class XNAFormsHook {

        public readonly IntPtr HandleForm;
        public IntPtr HandleHook { get; private set; }
        private Win32.WndProcDelegate _WndProcHook;

        public XNAFormsHook(IntPtr handleForm, HookDelegate hook) {
            HandleForm = handleForm;
            Hook = hook;
            _WndProcHook = WndProcHook;
            HandleHook = Win32.SetWindowsHookEx(
                Win32.HookType.WH_GETMESSAGE, // Was WH_CALLWNDPROC in ImGuiXNA
                _WndProcHook,
                IntPtr.Zero,
                Win32.GetWindowThreadProcessId(HandleForm, IntPtr.Zero)
            );
        }

        ~XNAFormsHook() {
            Dispose(false);
        }

        private int WndProcHook(int nCode, IntPtr wParam, ref Win32.Message lParam) {
            if (nCode >= 0 && ((int) wParam) == 1) {
                Win32.TranslateMessage(ref lParam);
                Hook?.Invoke(ref lParam);
            }

            return Win32.CallNextHookEx(HandleHook, nCode, wParam, ref lParam);
        }

        internal delegate void HookDelegate(ref Win32.Message msg);
        public HookDelegate Hook;

        public void Dispose() => Dispose(true);
        private void Dispose(bool disposing) {
            if (HandleHook == IntPtr.Zero)
                return;

            Win32.UnhookWindowsHookEx(HandleHook);
            HandleHook = IntPtr.Zero;
        }

    }

    internal static partial class Win32 {

        internal const int WM_CHAR = 0x0102;
        internal const int WM_KEYDOWN = 0x0100;

        internal enum HookType : int {
            WH_JOURNALRECORD = 0,
            WH_JOURNALPLAYBACK = 1,
            WH_KEYBOARD = 2,
            WH_GETMESSAGE = 3,
            WH_CALLWNDPROC = 4,
            WH_CBT = 5,
            WH_SYSMSGFILTER = 6,
            WH_MOUSE = 7,
            WH_HARDWARE = 8,
            WH_DEBUG = 9,
            WH_SHELL = 10,
            WH_FOREGROUNDIDLE = 11,
            WH_CALLWNDPROCRET = 12,
            WH_KEYBOARD_LL = 13,
            WH_MOUSE_LL = 14
        }

        internal struct Message {
            public IntPtr HWnd;
            public uint Msg;
            public IntPtr WParam;
            public IntPtr LParam;
            public IntPtr Result;
        }

        internal delegate int WndProcDelegate(int nCode, IntPtr wParam, ref Message m);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr SetWindowsHookEx(HookType hook, WndProcDelegate callback, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, ref Message m);

        [DllImport("user32.dll", EntryPoint = "TranslateMessage")]
        internal extern static bool TranslateMessage(ref Message m);

        [DllImport("user32.dll")]
        internal extern static uint GetWindowThreadProcessId(IntPtr window, IntPtr module);

    }
}
