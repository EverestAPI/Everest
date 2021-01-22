using Monocle;

namespace Celeste.Mod {
    public static class TouchInputNodes {

        public class Button : patch_VirtualButton_InputV2.Node {
            public readonly ATouchRegion Region;
            public Button(ATouchRegion region) {
                Region = region;
            }
            public override bool Check => !MInput.Disabled && TouchInputManager.IsTouch && Region.Touch.State.IsDown();
            public override bool Pressed => !MInput.Disabled && TouchInputManager.IsTouch && Region.Touch.State.IsDown() && Region.TouchPrev.State.IsUp();
            public override bool Released => !MInput.Disabled && TouchInputManager.IsTouch && Region.Touch.State.IsUp() && Region.TouchPrev.State.IsDown();
        }

    }
}
