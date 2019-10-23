#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;
using System.IO;
using FMOD.Studio;
using Monocle;
using Celeste.Mod.Meta;

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

        private static bool SpeedrunToolInstalled => _SpeedrunToolInstalled.Value;
        private static readonly int ZoomIntervalFrames = 6;

        private static Camera Camera;
        private static AreaKey area;
        private Vector2 mousePosition;

        private Session CurrentSession;
        private int zoomWaitFrames;

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
        
        [MonoModIgnore]
        private extern LevelTemplate TestCheck(Vector2 point);

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
    }
}