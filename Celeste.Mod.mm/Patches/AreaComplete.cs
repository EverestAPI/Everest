#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Jdenticon;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Celeste {
    class patch_AreaComplete : AreaComplete {

        private static string versionFull;
        private static float versionOffset;
        private static Texture2D identicon;
        private static float everestTime;
        private static bool isPieScreen; // on the pie screen, we should display the jdenticon on the left side of the screen, instead of the middle.

        private float buttonTimerDelay;
        private float buttonTimerEase;

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

            InitAreaCompleteInfoForEverest2(false, Session);

            buttonTimerDelay = 2.2f;
            buttonTimerEase = 0f;
        }

        // Backwards compatibility with Spring Collab 2020 and possibly other mods.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void InitAreaCompleteInfoForEverest(bool pieScreen) {
            InitAreaCompleteInfoForEverest2(pieScreen, null);
        }

        public static void InitAreaCompleteInfoForEverest2(bool pieScreen, Session session) {
            versionOffset = 0;
            if (Everest.Flags.IsDisabled)
                return;

            if (Settings.Instance.SpeedrunClock > SpeedrunType.Off) {
                versionFull = $"{Celeste.Instance.Version}\n{Everest.Build}";

                if (session != null &&
                    Everest.Content.TryGet($"Maps/{AreaData.Get(session).Mode[(int) session.Area.Mode].Path}", out ModAsset asset) &&
                    asset.Source.Mod?.Multimeta?.Length >= 1) {
                    versionFull = $"{versionFull}\n{asset.Source.Mod.Multimeta[0].Version}";
                    versionOffset -= 32;
                }

                identicon?.Dispose();
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

        public extern void orig_Update();
        public override void Update() {
            orig_Update();

            buttonTimerDelay -= Engine.DeltaTime;
            if (buttonTimerDelay <= 0f) {
                buttonTimerEase = Calc.Approach(buttonTimerEase, 1f, Engine.DeltaTime * 4f);
            }
        }

        private extern void orig_RenderUI();
        private void RenderUI() {
            orig_RenderUI();

            if (buttonTimerEase > 0f && Settings.Instance.SpeedrunClock == SpeedrunType.Off) {
                MTexture button = Input.GuiButton(Input.MenuConfirm);

                Vector2 pos = new Vector2(1860f - button.Width, 1020f - button.Height);
                float alpha = buttonTimerEase * buttonTimerEase;
                float scale = (0.9f + buttonTimerEase * 0.1f);

                for (int x = -1; x <= 1; x++) {
                    for (int y = -1; y <= 1; y++) {
                        if (x != 0 && y != 0) {
                            button.DrawCentered(
                                pos + new Vector2(x, y),
                                Color.Black * alpha * alpha * alpha * alpha,
                                Vector2.One * scale
                            );
                        }
                    }
                }

                button.DrawCentered(
                    pos,
                    Color.White * alpha,
                    Vector2.One * scale
                );
            }
        }

        public static void DisposeAreaCompleteInfoForEverest() {
            if (Everest.Flags.IsDisabled)
                return;

            identicon?.Dispose();
            identicon = null;
        }

        [PatchAreaCompleteVersionNumberAndVariants]
        public static extern void orig_VersionNumberAndVariants(string version, float ease, float alpha);
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

        private string GetCustomCompleteScreenTitle() {
            MapMetaCompleteScreenTitle completeScreenTitle = AreaData.Get(Session.Area)?.GetMeta()?.CompleteScreen?.Title;
            if (completeScreenTitle == null) {
                return null;
            }
            string text = null;
            switch (Session.Area.Mode) {
                case AreaMode.Normal:
                    if (Session.FullClear) {
                        text = completeScreenTitle.FullClear;
                    } else {
                        text = completeScreenTitle.ASide;
                    }
                    break;
                case AreaMode.BSide:
                    text = completeScreenTitle.BSide;
                    break;
                case AreaMode.CSide:
                    text = completeScreenTitle.CSide;
                    break;
                default:
                    break;
            }
            if (text == null) {
                return null;
            }
            return Dialog.Clean(text);
        }
    }
}
