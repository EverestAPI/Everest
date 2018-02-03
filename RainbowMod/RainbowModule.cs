using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Rainbow {
    public class RainbowModule : EverestModule {

        public static RainbowModule Instance;

        public override Type SettingsType => typeof(RainbowModuleSettings);
        public static RainbowModuleSettings Settings => (RainbowModuleSettings) Instance._Settings;

        private readonly static MethodInfo m_GetHairColor = typeof(PlayerHair).GetMethod("GetHairColor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private readonly static MethodInfo m_GetTrailColor = typeof(Player).GetMethod("GetTrailColor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static int trailIndex = 0;

        public RainbowModule() {
            Instance = this;
        }

        public override void Load() {
            // Runtime hooks are quite different from static patches.
            orig_GetHairColor = RuntimeDetour.Detour<d_GetHairColor>(
                m_GetHairColor,
                (d_GetHairColor) GetHairColor
            );
            orig_GetTrailColor = RuntimeDetour.Detour<d_GetTrailColor>(
                m_GetTrailColor,
                (d_GetTrailColor) GetTrailColor
            );
        }

        public override void Unload() {
            // Let's just hope that nothing else detoured this, as this is depth-based...
            RuntimeDetour.Undetour(m_GetHairColor);
            RuntimeDetour.Undetour(m_GetTrailColor);
        }

        public delegate Color d_GetHairColor(PlayerHair self, int index);
        public static d_GetHairColor orig_GetHairColor;
        public static Color GetHairColor(PlayerHair self, int index) {
            Color colorOrig = orig_GetHairColor(self, index);
            if (!Settings.Enabled)
                return colorOrig;

            Color colorRainbow = ColorFromHSV((index / (float) self.GetSprite().HairCount) * 180f + self.GetWave() * 60f, 0.6f, 0.6f);
            return new Color(
                (colorOrig.R / 255f) * 0.3f + (colorRainbow.R / 255f) * 0.7f,
                (colorOrig.G / 255f) * 0.3f + (colorRainbow.G / 255f) * 0.7f,
                (colorOrig.B / 255f) * 0.3f + (colorRainbow.B / 255f) * 0.7f,
                colorOrig.A
            );
        }

        public delegate Color d_GetTrailColor(Player self, bool wasDashB);
        public static d_GetTrailColor orig_GetTrailColor;
        public static Color GetTrailColor(Player self, bool wasDashB) {
            if (!Settings.Enabled || self.Hair == null)
                return orig_GetTrailColor(self, wasDashB);

            return self.Hair.GetHairColor((trailIndex++) % self.Hair.GetSprite().HairCount);
        }

        // Conversion algorithms found randomly on the net - best source for HSV <-> RGB ever:tm:

        private static void ColorToHSV(Color c, out float h, out float s, out float v) {
            float r = c.R / 255f;
            float g = c.G / 255f;
            float b = c.B / 255f;
            float min, max, delta;
            min = Math.Min(Math.Min(r, g), b);
            max = Math.Max(Math.Max(r, g), b);
            v = max;
            delta = max - min;
            if (max != 0) {
                s = delta / max;

                if (r == max)
                    h = (g - b) / delta;
                else if (g == max)
                    h = 2 + (b - r) / delta;
                else
                    h = 4 + (r - g) / delta;
                h *= 60f;
                if (h < 0)
                    h += 360f;
            } else {
                s = 0f;
                h = 0f;
            }
        }

        private static Color ColorFromHSV(float hue, float saturation, float value) {
            int hi = (int) (Math.Floor(hue / 60f)) % 6;
            float f = hue / 60f - (float) Math.Floor(hue / 60f);

            value = value * 255;
            int v = (int) (value);
            int p = (int) (value * (1 - saturation));
            int q = (int) (value * (1 - f * saturation));
            int t = (int) (value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return new Color(255, v, t, p);
            else if (hi == 1)
                return new Color(255, q, v, p);
            else if (hi == 2)
                return new Color(255, p, v, t);
            else if (hi == 3)
                return new Color(255, p, q, v);
            else if (hi == 4)
                return new Color(255, t, p, v);
            else
                return new Color(255, v, p, q);
        }

    }
}
