using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using System.Collections.Generic;

namespace Celeste.Mod {
    public class TouchInputManager : DrawableGameComponent {

        public static bool IsTouch { get; set; }
        public static Vector2 TouchToUI { get; protected set; }

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

            Vector2 touchPosUIScale = TouchToUI = new Vector2(
                1920f / Celeste.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth, // TouchPanel.DisplayWidth,
                1080f / Celeste.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight // TouchPanel.DisplayHeight
            );

            HashSet<int> consumedIds = new HashSet<int>();

            /*
            Logger.Log(LogLevel.Verbose, "touch", "--------");
            Logger.Log(LogLevel.Verbose, "touch", $"Connected: {state.IsConnected}; IsTouch: {IsTouch}; Count: {state.Count}");
            for (int ti = 0; ti < state.Count; ti++) {
                Logger.Log(LogLevel.Verbose, "touch", $"[{ti}]: {state[ti].State} {state[ti].Position * touchPosUIScale}");
            }
            */

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

                bool consumed = region.Update(state, touchInside ?? default);
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
