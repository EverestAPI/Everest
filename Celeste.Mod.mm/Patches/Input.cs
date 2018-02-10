#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    static class patch_Input {

        public static extern void orig_Initialize();
        public static void Initialize() {
            orig_Initialize();
            Everest.Events.Input.Initialize();
        }

        public static extern void orig_Deregister();
        public static void Deregister() {
            orig_Deregister();
            Everest.Events.Input.Deregister();
        }

        // Replace all VirtualInput fields with properties, allowing for runtime detouring.
        // TODO: Automate this with a MonoModRule.
        
        [MonoModHook("Monocle.VirtualButton Celeste.Input::ESC_Unsafe")]
        public static VirtualButton ESC;
        [MonoModRemove]
        public static VirtualButton ESC_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::ESC")]
        public static VirtualButton ESC_Safe {
            get {
                return ESC_Unsafe;
            }
            set {
                ESC_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::Pause_Unsafe")]
        public static VirtualButton Pause;
        [MonoModRemove]
        public static VirtualButton Pause_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::Pause")]
        public static VirtualButton Pause_Safe {
            get {
                return Pause_Unsafe;
            }
            set {
                Pause_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuLeft_Unsafe")]
        public static VirtualButton MenuLeft;
        [MonoModRemove]
        public static VirtualButton MenuLeft_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuLeft")]
        public static VirtualButton MenuLeft_Safe {
            get {
                return MenuLeft_Unsafe;
            }
            set {
                MenuLeft_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuRight_Unsafe")]
        public static VirtualButton MenuRight;
        [MonoModRemove]
        public static VirtualButton MenuRight_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuRight")]
        public static VirtualButton MenuRight_Safe {
            get {
                return MenuRight_Unsafe;
            }
            set {
                MenuRight_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuUp_Unsafe")]
        public static VirtualButton MenuUp;
        [MonoModRemove]
        public static VirtualButton MenuUp_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuUp")]
        public static VirtualButton MenuUp_Safe {
            get {
                return MenuUp_Unsafe;
            }
            set {
                MenuUp_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuDown_Unsafe")]
        public static VirtualButton MenuDown;
        [MonoModRemove]
        public static VirtualButton MenuDown_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuDown")]
        public static VirtualButton MenuDown_Safe {
            get {
                return MenuDown_Unsafe;
            }
            set {
                MenuDown_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuConfirm_Unsafe")]
        public static VirtualButton MenuConfirm;
        [MonoModRemove]
        public static VirtualButton MenuConfirm_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuConfirm")]
        public static VirtualButton MenuConfirm_Safe {
            get {
                return MenuConfirm_Unsafe;
            }
            set {
                MenuConfirm_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuCancel_Unsafe")]
        public static VirtualButton MenuCancel;
        [MonoModRemove]
        public static VirtualButton MenuCancel_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuCancel")]
        public static VirtualButton MenuCancel_Safe {
            get {
                return MenuCancel_Unsafe;
            }
            set {
                MenuCancel_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuJournal_Unsafe")]
        public static VirtualButton MenuJournal;
        [MonoModRemove]
        public static VirtualButton MenuJournal_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::MenuJournal")]
        public static VirtualButton MenuJournal_Safe {
            get {
                return MenuJournal_Unsafe;
            }
            set {
                MenuJournal_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::QuickRestart_Unsafe")]
        public static VirtualButton QuickRestart;
        [MonoModRemove]
        public static VirtualButton QuickRestart_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::QuickRestart")]
        public static VirtualButton QuickRestart_Safe {
            get {
                return QuickRestart_Unsafe;
            }
            set {
                QuickRestart_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualIntegerAxis Celeste.Input::MoveX_Unsafe")]
        public static VirtualIntegerAxis MoveX;
        [MonoModRemove]
        public static VirtualIntegerAxis MoveX_Unsafe;
        [MonoModHook("Monocle.VirtualIntegerAxis Celeste.Input::MoveX")]
        public static VirtualIntegerAxis MoveX_Safe {
            get {
                return MoveX_Unsafe;
            }
            set {
                MoveX_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualIntegerAxis Celeste.Input::MoveY_Unsafe")]
        public static VirtualIntegerAxis MoveY;
        [MonoModRemove]
        public static VirtualIntegerAxis MoveY_Unsafe;
        [MonoModHook("Monocle.VirtualIntegerAxis Celeste.Input::MoveY")]
        public static VirtualIntegerAxis MoveY_Safe {
            get {
                return MoveY_Unsafe;
            }
            set {
                MoveY_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualJoystick Celeste.Input::Aim_Unsafe")]
        public static VirtualJoystick Aim;
        [MonoModRemove]
        public static VirtualJoystick Aim_Unsafe;
        [MonoModHook("Monocle.VirtualJoystick Celeste.Input::Aim")]
        public static VirtualJoystick Aim_Safe {
            get {
                return Aim_Unsafe;
            }
            set {
                Aim_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualJoystick Celeste.Input::MountainAim_Unsafe")]
        public static VirtualJoystick MountainAim;
        [MonoModRemove]
        public static VirtualJoystick MountainAim_Unsafe;
        [MonoModHook("Monocle.VirtualJoystick Celeste.Input::MountainAim")]
        public static VirtualJoystick MountainAim_Safe {
            get {
                return MountainAim_Unsafe;
            }
            set {
                MountainAim_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::Jump_Unsafe")]
        public static VirtualButton Jump;
        [MonoModRemove]
        public static VirtualButton Jump_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::Jump")]
        public static VirtualButton Jump_Safe {
            get {
                return Jump_Unsafe;
            }
            set {
                Jump_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::Dash_Unsafe")]
        public static VirtualButton Dash;
        [MonoModRemove]
        public static VirtualButton Dash_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::Dash")]
        public static VirtualButton Dash_Safe {
            get {
                return Dash_Unsafe;
            }
            set {
                Dash_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::Grab_Unsafe")]
        public static VirtualButton Grab;
        [MonoModRemove]
        public static VirtualButton Grab_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::Grab")]
        public static VirtualButton Grab_Safe {
            get {
                return Grab_Unsafe;
            }
            set {
                Grab_Unsafe = value;
            }
        }

        [MonoModHook("Monocle.VirtualButton Celeste.Input::Talk_Unsafe")]
        public static VirtualButton Talk;
        [MonoModRemove]
        public static VirtualButton Talk_Unsafe;
        [MonoModHook("Monocle.VirtualButton Celeste.Input::Talk")]
        public static VirtualButton Talk_Safe {
            get {
                return Talk_Unsafe;
            }
            set {
                Talk_Unsafe = value;
            }
        }

    }
}
