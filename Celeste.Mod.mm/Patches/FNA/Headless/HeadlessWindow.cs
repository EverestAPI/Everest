using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using System;

namespace Microsoft.Xna.Framework {
    namespace Graphics {
        // Expose internal FND3D bindings
        [MonoModIgnore]
        [GameDependencyPatch("FNA")]
        public static class FNA3D {
            public static extern uint FNA3D_PrepareWindowAttributes();
        }
    }

    // Expose internal GameWindow
    [MonoModIgnore]
    [GameDependencyPatch("FNA")]
    public abstract class GameWindow {
        public abstract bool AllowUserResizing { get; set; }
        public abstract Rectangle ClientBounds { get; }
        public abstract DisplayOrientation CurrentOrientation { get; internal set; }
        public abstract IntPtr Handle { get; }
        public abstract string ScreenDeviceName { get; }

        public abstract void BeginScreenDeviceChange(bool willBeFullScreen);
        public abstract void EndScreenDeviceChange(string screenDeviceName, int clientWidth, int clientHeight);

        protected internal abstract void SetSupportedOrientations(DisplayOrientation orientations);
        protected abstract void SetTitle(string title);
    }

    [GameDependencyPatch("FNA")]
    public class HeadlessWindow : GameWindow {
        public override bool AllowUserResizing {
            get => false;
            set {}
        }

        public override Rectangle ClientBounds => Rectangle.Empty;

        public override DisplayOrientation CurrentOrientation {
            get => DisplayOrientation.Default;
            internal set {}
        }

        public override IntPtr Handle => IntPtr.Zero;
        public override string ScreenDeviceName => string.Empty;

        public HeadlessWindow() {
            // Needs to be called before the graphics device is created
            FNA3D.FNA3D_PrepareWindowAttributes();
        }

        public override void BeginScreenDeviceChange(bool willBeFullScreen) {
            // Stub
        }

        public override void EndScreenDeviceChange(string screenDeviceName, int clientWidth, int clientHeight) {
            // Stub
        }

        protected internal override void SetSupportedOrientations(DisplayOrientation orientations) {
            // Stub
        }

        protected override void SetTitle(string title) {
            // Stub
        }
    }
}
