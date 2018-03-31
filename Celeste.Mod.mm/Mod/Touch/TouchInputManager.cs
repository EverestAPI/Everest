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
    public class TouchInputManager : DrawableGameComponent {

        public static bool IsTouch;

        public static TouchInputManager Instance;

        public List<ATouchRegion> Regions = new List<ATouchRegion>();

        public TouchInputManager(Game game)
            : base(game) {
            Instance = this;

            UpdateOrder = -50000;
            DrawOrder = 50000;

            IsTouch = TouchPanel.GetCapabilities().IsConnected;
        }

        public override void Update(GameTime gameTime) {
            TouchCollection state = TouchPanel.GetState();
            if (state.IsConnected && state.Count > 0) {
                IsTouch = true;
            }

            Vector2 touchPosUIScale = new Vector2(
                1920f / TouchPanel.DisplayWidth,
                1080f / TouchPanel.DisplayHeight
            );

            HashSet<int> consumedIds = new HashSet<int>();

            for (int i = 0; i < Regions.Count; i++) {
                ATouchRegion region = Regions[i];
                if (region == null)
                    continue;

                TouchLocation? touchInside = null;
                for (int ti = 0; ti < state.Count; ti++) {
                    TouchLocation touch = state[ti];
                    if (consumedIds.Contains(touch.Id))
                        continue;
                    Vector2 touchPosUI = touch.Position * touchPosUIScale;
                    if (touchPosUI.X < region.Position.X - region.Size.X * 0.5f ||
                        touchPosUI.X > region.Position.X + region.Size.X * 0.5f ||
                        touchPosUI.Y < region.Position.Y - region.Size.Y * 0.5f ||
                        touchPosUI.Y > region.Position.Y + region.Size.Y * 0.5f)
                        continue;
                    touchInside = touch;
                }

                bool consumed = region.Update(state, touchInside ?? default(TouchLocation));
                if (consumed && touchInside != null)
                    consumedIds.Add(touchInside.Value.Id);
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime) {
            base.Draw(gameTime);

            for (int i = 0; i < Regions.Count; i++) {
                ATouchRegion region = Regions[i];
                if (region == null)
                    continue;
                region.Draw();
            }
        }

    }
}
