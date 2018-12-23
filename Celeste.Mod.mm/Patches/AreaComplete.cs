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
        private static float identiconSine;

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

            if (Everest.Flags.Disabled)
                return;

            versionFull = $"{Celeste.Instance.Version}\n{Everest.BuildString}";

            using (Stream stream = Identicon.FromValue(Everest.InstallationHash, size: 100).SaveAsPng())
                identicon = Texture2D.FromStream(Celeste.Instance.GraphicsDevice, stream);
        }

        public override void End() {
            base.End();

            if (Everest.Flags.Disabled)
                return;

            identicon.Dispose();
        }

        public static extern void orig_VersionNumberAndVariants(string version, float ease, float alpha);
        [MonoModNoNew]
        public static void VersionNumberAndVariants(string version, float ease, float alpha) {
            if (Everest.Flags.Disabled) {
                orig_VersionNumberAndVariants(version, ease, alpha);
                return;
            }

            orig_VersionNumberAndVariants(versionFull, ease, alpha);

            identiconSine += Engine.RawDeltaTime;
            const float amplitude = 4f;
            const int sliceSize = 3;
            const float sliceAdd = 0.13f;

            Vector2 position = new Vector2(1920f * 0.5f, 1080f - 150f);
            Rectangle clipRect = identicon.Bounds;
            clipRect.Height = sliceSize;
            int i = 0;
            while (clipRect.Y < identicon.Height) {
                Vector2 offs = new Vector2((float) Math.Round(Math.Sin(identiconSine + sliceAdd * i) * amplitude), sliceSize * i);
                Draw.SpriteBatch.Draw(identicon, position, clipRect, Color.White * ease, 0f, Vector2.One * 0.5f - offs, 1f, SpriteEffects.None, 0f);
                i++;
                clipRect.Y += sliceSize;
                clipRect.Height = Math.Min(sliceSize, identicon.Height - clipRect.Y);
            }
        }

    }
}
