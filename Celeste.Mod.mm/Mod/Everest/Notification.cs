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
    /// <summary>
    /// The base class for all Everest notifications.
    /// </summary>
    public abstract class ANotification : IDisposable {

        /// <summary>
        /// Size of your notification in UI-space (1920x1080). Affects and affected by position of other notifications.
        /// </summary>
        public Vector2 Size = new Vector2(256f, 128f);

        /// <summary>
        /// For how long should the notification be visible?
        /// </summary>
        public float Duration = 10f;

        /// <summary>
        /// Position of the notification when drawing. Updated by the Everest NotificationManager.
        /// </summary>
        public Vector2 Position;

        /// <summary>
        /// Time in seconds since the creation of the notification. Updated by the Everest NotificationManager.
        /// </summary>
        public float Time;

        public ANotification() {
            NotificationManager.Instance.Notifications.Add(this);
        }

        public abstract void Update();

        public abstract void Draw();

        protected virtual void Dispose(bool disposing) {

        }

        public void Dispose() {
            Dispose(true);
        }

    }
    /// <summary>
    /// The class for basic notifications. If you want to render fully custom notifications, override ANotification instead.
    /// </summary>
    public class Notification : ANotification {

        public MTexture Background;

        public Color ColorBackground = Color.DarkSlateBlue;
        public Color ColorForeground = Color.White;

        public string Title;
        public string Description;

        public MTexture Icon;

        public Notification() {

        }

        public override void Update() {

        }

        public override void Draw() {
            if (Background == null)
                Background = GFX.Gui["notification/default/bg.png"];

            Background.Draw(
                Position,
                Vector2.Zero,
                ColorBackground,
                new Vector2(
                    Size.X / Background.Width,
                    Size.Y / Background.Height
                )
            );
        }

    }
}
