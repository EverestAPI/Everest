#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Jdenticon;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_AreaComplete : AreaComplete {

        private static string versionFull;
        private static Texture2D identicon;
        private static float everestTime;
        private static bool isPieScreen; // on the pie screen, we should display the jdenticon on the left side of the screen, instead of the middle.

        public patch_AreaComplete(Session session, XmlElement xml, Atlas atlas, HiresSnow snow, MapMetaCompleteScreen meta)
            : base(session, xml, atlas, snow) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Patching constructors is ugly.
        [MonoModConstructor]
        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchAreaCompleteCtor] // ... except for manually manipulating the method via MonoModRules
        public extern void ctor(Session session, XmlElement xml, Atlas atlas, HiresSnow snow);

        public override void Begin() {
            base.Begin();

            InitAreaCompleteInfoForEverest(pieScreen: false);
        }

        public static void InitAreaCompleteInfoForEverest(bool pieScreen) {
            if (Everest.Flags.IsDisabled)
                return;

            if (Settings.Instance.SpeedrunClock > SpeedrunType.Off) {
                versionFull = $"{Celeste.Instance.Version}\n{Everest.Build}";

                using (Stream stream = Identicon.FromHash(Everest.InstallationHash, 100).SaveAsPng())
                    identicon = Texture2D.FromStream(Celeste.Instance.GraphicsDevice, stream);
            }

            isPieScreen = pieScreen;
        }

        public extern void orig_End();
        public override void End() {
            orig_End();

            DisposeAreaCompleteInfoForEverest();
        }

        public static void DisposeAreaCompleteInfoForEverest() {
            if (Everest.Flags.IsDisabled)
                return;

            identicon?.Dispose();
            identicon = null;
        }

        public static extern void orig_VersionNumberAndVariants(string version, float ease, float alpha);
        [MonoModNoNew]
        public static new void VersionNumberAndVariants(string version, float ease, float alpha) {
            if (Everest.Flags.IsDisabled) {
                orig_VersionNumberAndVariants(version, ease, alpha);
                return;
            }

            everestTime += Engine.RawDeltaTime;

            orig_VersionNumberAndVariants(versionFull, ease, alpha);

            if (identicon == null)
                return;

            const float amplitude = 5f;
            const int sliceSize = 2;
            const float sliceAdd = 0.12f;

            float waveStart = everestTime * 1.3f;
            float rotation = MathHelper.Pi * 0.02f * (float) Math.Sin(everestTime * 0.8f);

            Vector2 position = new Vector2(1920f * (isPieScreen ? 0.05f : 0.5f), 1080f - 150f);
            Rectangle clipRect = identicon.Bounds;
            clipRect.Height = sliceSize;
            int i = 0;
            while (clipRect.Y < identicon.Height) {
                Vector2 offs = new Vector2(identicon.Width * 0.5f + (float) Math.Round(amplitude * 0.5f + amplitude * 0.5f * Math.Sin(everestTime + sliceAdd * i)), sliceSize * -i);
                Draw.SpriteBatch.Draw(identicon, position, clipRect, Color.White * ease, rotation, offs, 1f, SpriteEffects.None, 0f);
                i++;
                clipRect.Y += sliceSize;
                clipRect.Height = Math.Min(sliceSize, identicon.Height - clipRect.Y);
            }
        }

    }
}
