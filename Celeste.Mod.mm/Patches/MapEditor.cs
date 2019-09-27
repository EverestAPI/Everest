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

        private static Camera Camera;
        private static AreaKey area;

        private Session CurrentSession;

        public patch_MapEditor(AreaKey area, bool reloadMapData = true)
            : base(area, reloadMapData) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(AreaKey area, bool reloadMapData = true);
        [MonoModConstructor]
        public void ctor(AreaKey area, bool reloadMapData = true) {
            AreaKey prevArea = patch_MapEditor.area;

            orig_ctor(area, reloadMapData);

            Session prevSession = CurrentSession;
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

    }
}
