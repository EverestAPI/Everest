using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Helpers;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class NotificationManager : DrawableGameComponent {

        public static NotificationManager Instance;

        public List<ANotification> Notifications = new List<ANotification>();

        public NotificationManager(Game game)
            : base(game) {
            Instance = this;

            UpdateOrder = 10000;
            DrawOrder = 10000;
        }

        public override void Update(GameTime gameTime) {
            int y = 1080;
            for (int i = 0; i < Notifications.Count; i++) {
                ANotification note = Notifications[i];
                if (note == null)
                    continue;

                note.Time += Engine.RawDeltaTime;
                if (note.Time > note.Duration) {
                    note.Dispose();
                    Notifications.RemoveAt(i);
                    i--;
                    continue;
                }

                note.Update();

                int height = (int) note.Size.Y;
                y -= height;
                note.Position = new Vector2(1920 - note.Size.X, y);
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime) {
            base.Draw(gameTime);

            for (int i = 0; i < Notifications.Count; i++) {
                ANotification note = Notifications[i];
                if (note == null)
                    continue;
                note.Draw();
            }
        }

    }
}
