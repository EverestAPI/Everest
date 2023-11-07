using Celeste.Mod.Meta;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Mod.Entities {
    public class CustomTextVignette : Scene {
        public bool CanPause => menu == null;

        private Session session;
        private EventInstance sfx;
        private string areaMusic;

        private TextMenu menu;

        private bool started;
        private bool exiting;

        private float fade;
        private float pauseFade;
        private float initialDelay;
        private float finalDelay;

        private FancyText.Text text;
        private int textStart;
        private float textAlpha;
        private Coroutine textCoroutine;

        private HudRenderer renderer;
        private HiresSnow snow;

        public CustomTextVignette(Session session, MapMetaTextVignette meta, HiresSnow snow = null) {
            this.session = session;
            areaMusic = session.Audio.Music.Event;
            session.Audio.Music.Event = null;
            session.Audio.Apply();

            sfx = Audio.Play(meta.Audio);

            if (snow == null) {
                fade = 1f;
                snow = new HiresSnow();
            }
            snow.Direction = meta.SnowDirection;
            Add(renderer = new HudRenderer());
            Add(this.snow = snow);
            ((patch_RendererList) (object) RendererList).UpdateLists();

            initialDelay = meta.InitialDelay;
            finalDelay = meta.FinalDelay;

            text = FancyText.Parse(Dialog.Get(meta.Dialog), 960, 8, 0f);
            textCoroutine = new Coroutine(TextSequence());
        }

        public CustomTextVignette(Session session, string text, HiresSnow snow = null) // maintain interface for backwards compatibility
            : this(session, new MapMetaTextVignette {Dialog = text}, snow) { }

        private IEnumerator TextSequence() {
            yield return initialDelay;

            while (textStart < text.Count) {
                textAlpha = 1f;
                int charactersOnPage = text.GetCharactersOnPage(textStart);
                float fadeTimePerCharacter = 1f / charactersOnPage;
                for (int i = textStart; i < text.Count && !(text[i] is FancyText.NewPage); i++) {
                    if (text[i] is FancyText.Char c) {
                        while ((c.Fade += Engine.DeltaTime / fadeTimePerCharacter) < 1f)
                            yield return null;

                        c.Fade = 1f;
                    }
                }
                yield return 2.5f;

                while (textAlpha > 0f) {
                    textAlpha -= 1f * Engine.DeltaTime;
                    yield return null;
                }

                textAlpha = 0f;
                textStart = text.GetNextPageStart(textStart);
                yield return 0.5f;
            }
            if (finalDelay > 0) {
                yield return finalDelay;
            }
            if (!started) {
                StartGame();
            }
            textStart = int.MaxValue;
        }

        public override void Update() {
            if (menu == null) {
                base.Update();
                if (!exiting) {
                    if (textCoroutine != null && textCoroutine.Active) {
                        textCoroutine.Update();
                    }
                    if (menu == null && (Input.Pause.Pressed || Input.ESC.Pressed)) {
                        Input.Pause.ConsumeBuffer();
                        Input.ESC.ConsumeBuffer();
                        OpenMenu();
                    }
                }
            } else if (!exiting) {
                menu.Update();
            }

            pauseFade = Calc.Approach(pauseFade, (menu != null) ? 1 : 0, Engine.DeltaTime * 8f);
            renderer.BackgroundFade = Calc.Approach(renderer.BackgroundFade, (menu != null) ? 0.6f : 0f, Engine.DeltaTime * 3f);
            fade = Calc.Approach(fade, 0f, Engine.DeltaTime);
        }

        public void OpenMenu() {
            Audio.Play(SFX.ui_game_pause);
            Audio.Pause(sfx);
            Add(menu = new TextMenu());
            menu.Add(new TextMenu.Button(Dialog.Clean("intro_vignette_resume")).Pressed(CloseMenu));
            menu.Add(new TextMenu.Button(Dialog.Clean("intro_vignette_skip")).Pressed(StartGame));
            menu.Add(new TextMenu.Button(Dialog.Clean("intro_vignette_quit")).Pressed(ReturnToMap));
            menu.OnCancel = menu.OnESC = menu.OnPause = CloseMenu;
        }

        private void CloseMenu() {
            Audio.Play(SFX.ui_game_unpause);
            Audio.Resume(sfx);
            menu?.RemoveSelf();
            menu = null;
        }

        private void StartGame() {
            textCoroutine = null;
            StopSfx();
            session.Audio.Music.Event = areaMusic;

            if (menu != null) {
                menu.RemoveSelf();
                menu = null;
            }

            new FadeWipe(this, false, () => Engine.Scene = new LevelLoader(session))
                .OnUpdate = (f) => textAlpha = Math.Min(textAlpha, 1f - f);

            started = true;
            exiting = true;
        }

        private void ReturnToMap() {
            StopSfx();
            menu.RemoveSelf();
            menu = null;
            exiting = true;
            bool toAreaQuit = SaveData.Instance.Areas[0].Modes[0].Completed && Celeste.PlayMode != Celeste.PlayModes.Event;
            new FadeWipe(this, false, delegate {
                if (toAreaQuit) {
                    Engine.Scene = new OverworldLoader(Overworld.StartMode.AreaQuit, snow);
                } else {
                    Engine.Scene = new OverworldLoader(Overworld.StartMode.Titlescreen, snow);
                }
            }).OnUpdate = (f) => textAlpha = Math.Min(textAlpha, 1f - f);

            ((patch_RendererList) (object) RendererList).UpdateLists();
            RendererList.MoveToFront(snow);
        }

        private void StopSfx() {
            Audio.Stop(sfx, false);
        }

        public override void End() {
            StopSfx();
            base.End();
        }

        public override void Render() {
            base.Render();
            if (fade > 0f || textAlpha > 0f) {
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, RasterizerState.CullNone, null, Engine.ScreenMatrix);

                if (fade > 0f) {
                    Draw.Rect(-1f, -1f, Celeste.TargetWidth + 2f, Celeste.TargetHeight + 2f, Color.Black * fade);
                }

                if (textStart < text.Nodes.Count && textAlpha > 0f) {
                    text.DrawJustifyPerLine(new Vector2(Celeste.TargetWidth, Celeste.TargetHeight) * 0.5f, new Vector2(0.5f, 0.5f), Vector2.One, textAlpha * (1f - pauseFade), textStart);
                }

                Draw.SpriteBatch.End();
            }
        }

    }
}
