using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.Helpers {
    /// <summary>
    /// Exposes static functions that allow you to easily check if something is currently visible, to be used for culling.
    /// </summary>
    public static class CullHelper {
        /// <summary>
        /// Checks whether the rectangle (x, y, w, h) is visible inside of the camera
        /// </summary>
        public static bool IsRectangleVisible(float x, float y, float w, float h, float lenience = 4f, Camera camera = null) {
            camera ??= (Engine.Scene as Level)?.Camera;
            if (camera is null) {
                return true;
            }

            return x + w >= camera.Left - lenience
                && x <= camera.Right + lenience
                && y + h >= camera.Top - lenience
                && y <= camera.Bottom + 180f + lenience;
        }

        /// <summary>
        /// Checks if the curve is visible by creating a rectangle containing the edge points of the curve (Begin, Control, End)
        /// </summary>
        public static bool IsCurveVisible(SimpleCurve curve, float lenience = 8f, Camera camera = null) {
            Vector2 a = curve.Begin;
            Vector2 b = curve.Control;
            Vector2 c = curve.End;

            float left = Math.Min(a.X, Math.Min(b.X, c.X));
            float right = Math.Max(a.X, Math.Max(b.X, c.X));
            float top = Math.Min(a.Y, Math.Min(b.Y, c.Y));
            float bottom = Math.Max(a.Y, Math.Max(b.Y, c.Y));

            return IsRectangleVisible(left, top, right - left, bottom - top, lenience, camera);
        }
    }
}
