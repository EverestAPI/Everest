#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;
using Microsoft.Xna.Framework.Graphics;

namespace Celeste.Editor {
    class patch_MapEditor : MapEditor {
        private static readonly Lazy<bool> _SpeedrunToolInstalled = new Lazy<bool>(() =>
            Everest.Modules.Any(module => {
                EverestModuleMetadata meta = module?.Metadata;
                if (meta == null || meta.Version == null)
                    return false;
                return meta.Name == "SpeedrunTool" && meta.Version <= new Version(1, 6, 7, 0);
            })
        );

        private const string ManualText = "Right Click:  Teleport to the room\n" +
                                          "Confirm:      Teleport to the room\n" +
                                          "Hold Control: Restart Chapter before teleporting\n" +
                                          "Hold Shift:   Teleport to the mouse position\n" +
                                          "Cancel:       Exit debug map\n" +
                                          "Q:            Show red berries\n" +
                                          "F1:           Show keys\n" +
                                          "F5:           Show/Hide instructions";

        private const string MinimalManualText = "F5: Show/Hide instructions";

        private static bool SpeedrunToolInstalled => _SpeedrunToolInstalled.Value;
        private static readonly int ZoomIntervalFrames = 6;

        private static Camera Camera;
        private static AreaKey area;
        private Vector2 mousePosition;
        private MapData mapData;
        private List<LevelTemplate> levels;

        private Session CurrentSession;
        private int zoomWaitFrames;
        private List<Vector2> keys;

        public patch_MapEditor(AreaKey area, bool reloadMapData = true)
            : base(area, reloadMapData) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(AreaKey area, bool reloadMapData = true);
        [MonoModConstructor]
        public void ctor(AreaKey area, bool reloadMapData = true) {
            AreaKey prevArea = patch_MapEditor.area;

            orig_ctor(area, reloadMapData);

            CurrentSession = (Engine.Scene as Level)?.Session ?? SaveData.Instance?.CurrentSession;
            if (CurrentSession == null || CurrentSession.Area != area) {
                CurrentSession = null;
                return;
            }

            if (prevArea == area)
                return;

            Vector2 pos;
            if (CurrentSession.RespawnPoint != null) {
                pos = CurrentSession.RespawnPoint.Value;
            } else {
                Point lvlCenter = CurrentSession.LevelData.Bounds.Center;
                pos = new Vector2(lvlCenter.X, lvlCenter.Y);
            }

            Camera.CenterOrigin();
            Camera.Zoom = 6f;
            Camera.Position = pos / 8f;
        }

        [MonoModIgnore]
        private extern void Save();

        [MonoModIgnore]
        private extern LevelTemplate TestCheck(Vector2 point);

        private void LoadLevel(LevelTemplate level, Vector2 at) {
            Save();

            KeyboardState keys = Keyboard.GetState();

            Session session = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl) ? null : CurrentSession;
            session = session ?? new Session(area);
            session.FirstLevel = false;
            session.StartedFromBeginning = false;
            session.Level = level.Name;
            session.RespawnPoint = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift) ? at : (Vector2?) null;

            Engine.Scene = new LevelLoader(session, at);
        }

        public extern void orig_Update();

        public override void Update() {
            if (!SpeedrunToolInstalled) {
                MakeMapEditorBetter();
            }

            orig_Update();
        }

        private void MakeMapEditorBetter() {
            // press cancel button to return game
            if ((Input.ESC.Pressed || Input.MenuCancel.Pressed) && CurrentSession != null) {
                Input.ESC.ConsumePress();
                Input.MenuCancel.ConsumePress();
                Engine.Scene = new LevelLoader(CurrentSession);
            }

            // press confirm button to teleport to selected room
            if (Input.MenuConfirm.Pressed) {
                Input.MenuConfirm.ConsumePress();
                LevelTemplate level = TestCheck(mousePosition);
                if (level != null) {
                    if (level.Type == LevelTemplateType.Filler) {
                        return;
                    }

                    LoadLevel(level, mousePosition * 8f);
                }
            }

            // speed up camera when zoom out
            if (Camera != null && Camera.Zoom < 6f) {
                Camera.Position += new Vector2(Input.MoveX.Value, Input.MoveY.Value) * 300f * Engine.DeltaTime *
                                   ((float) Math.Pow(1.3, 6 - Camera.Zoom) - 1);
            }

            // controller right stick zoom the map
            GamePadState currentState = MInput.GamePads[Input.Gamepad].CurrentState;
            if (zoomWaitFrames <= 0 && Camera != null) {
                float newZoom = 0f;
                if (Math.Abs(currentState.ThumbSticks.Right.X) >= 0.5f) {
                    newZoom = Camera.Zoom + Math.Sign(currentState.ThumbSticks.Right.X) * 1f;
                } else if (Math.Abs(currentState.ThumbSticks.Right.Y) >= 0.5f) {
                    newZoom = Camera.Zoom + Math.Sign(currentState.ThumbSticks.Right.Y) * 1f;
                }

                if (newZoom >= 1f) {
                    Camera.Zoom = newZoom;
                    zoomWaitFrames = ZoomIntervalFrames;
                }
            }
        }

        public extern void orig_Render();
        public override void Render() {
            orig_Render();
            RenderManualText();
            RenderKeys();
            RenderHighlightCurrentRoom();
        }

        private void RenderManualText() {
            if (MInput.Keyboard.Pressed(Keys.F5)) {
                CoreModule.Settings.ShowManualTextOnDebugMap = !CoreModule.Settings.ShowManualTextOnDebugMap;
            }

            Draw.SpriteBatch.Begin();

            string text = MinimalManualText;
            if (CoreModule.Settings.ShowManualTextOnDebugMap) {
                text = ManualText;
            }

            Vector2 textSize = Draw.DefaultFont.MeasureString(text);
            Draw.Rect(Engine.ViewWidth - textSize.X - 20, Engine.ViewHeight - textSize.Y - 20f,
                textSize.X + 20f, textSize.Y + 20f, Color.Black * 0.8f);
            Draw.SpriteBatch.DrawString(
                Draw.DefaultFont,
                text,
                new Vector2(Engine.ViewWidth - textSize.X - 10, Engine.ViewHeight - textSize.Y - 10f),
                Color.White
            );

            Draw.SpriteBatch.End();
        }

        private void RenderKeys() {
            if (keys == null && mapData?.Levels != null) {
                keys = new List<Vector2>();
                foreach (LevelData levelData in mapData.Levels) {
                    Rectangle bounds = levelData.Bounds;
                    Vector2 basePosition = new Vector2(bounds.X, bounds.Y);
                    IEnumerable<EntityData> keyEntityDatas = levelData.Entities
                        .Where(entityData => entityData.Name == "key");
                    foreach (EntityData keyEntityData in keyEntityDatas) {
                        keys.Add((basePosition + keyEntityData.Position) / 8);
                    }
                }
            }

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Camera.Matrix * Engine.ScreenMatrix);
            if (keys != null && keys.Count > 0) {
                for (int i = 0; i < keys.Count; i++) {
                    Draw.HollowRect(keys[i].X - 1f, keys[i].Y - 2f, 3f, 3f, Color.Gold);
                }
            }
            Draw.SpriteBatch.End();

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Engine.ScreenMatrix);
            if (MInput.Keyboard.Check(Keys.F1)) {
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.25f);
                if (keys != null && keys.Count > 0) {
                    for (int i = 0; i < keys.Count; i++) {
                        ActiveFont.DrawOutline((i + 1).ToString(),
                            (keys[i] - Camera.Position + Vector2.UnitX) *
                            Camera.Zoom + new Vector2(960f, 540f), new Vector2(0.5f, 0.5f), Vector2.One * 1f, Color.Gold,
                            2f, Color.Black);

                    }
                }
            }
            Draw.SpriteBatch.End();
        }

        private void RenderHighlightCurrentRoom() {
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Camera.Matrix * Engine.ScreenMatrix);
            if (CurrentSession != null) {
                LevelTemplate currentTemplate = levels.Find(template => template.Name == CurrentSession.Level);
                currentTemplate?.RenderHighlight(Camera, false, true);
            }
            Draw.SpriteBatch.End();
        }
    }
}