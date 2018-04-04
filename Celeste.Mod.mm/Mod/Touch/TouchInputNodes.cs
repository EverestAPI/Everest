using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using Monocle;
using MonoMod.Helpers;
using MonoMod.InlineRT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class TouchInputNodes {

        public class Button : VirtualButton.Node {
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
