using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using Monocle;
using MonoMod.Helpers;
using MonoMod.InlineRT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    /// <summary>
    /// The base class for all Everest touch regions.
    /// </summary>
    public abstract class ATouchRegion {

        /// <summary>
        /// Center position of your touch region in UI-space (1920x1080).
        /// </summary>
        public Vector2 Position;

        /// <summary>
        /// Size of your touch region in UI-space (1920x1080).
        /// </summary>
        public Vector2 Size = new Vector2(256f, 256f);

        /// <summary>
        /// The currently registered touch in the region.
        /// </summary>
        public virtual TouchLocation Touch { get; protected set; }

        /// <summary>
        /// The previously registered touch in the region.
        /// </summary>
        public virtual TouchLocation TouchPrev { get; protected set; }

        public ATouchRegion() {
            TouchInputManager.Instance.Regions.Add(this);
        }

        public abstract bool Update(TouchCollection state, TouchLocation touch);

        public abstract void Draw();

    }
    /// <summary>
    /// Basic touch regions. If you want to render fully custom regions, override ATouchRegion instead.
    /// </summary>
    public class TouchRegion : ATouchRegion {

        public MTexture Icon;

        public delegate bool TouchRegionEventHandler(TouchRegion region, TouchLocation touch);
        public event TouchRegionEventHandler OnTouch;

        public delegate bool TouchRegionCondition(TouchRegion region);
        public TouchRegionCondition Condition;

        public VirtualButton Button {
            set {
                value.Nodes.Add(new TouchInputNodes.Button(this));
            }
        }

        public Color ColorUp = Color.DarkSlateBlue * 0.5f;
        public Color ColorDown = Color.White;

        private float alphaTime;
        private float alpha;

        private float colorTime;
        private Color color;

        public TouchRegion()
            : base() {
        }

        public override bool Update(TouchCollection state, TouchLocation touch) {
            bool enabled = TouchInputManager.IsTouch;
            if (enabled && Condition != null)
                enabled = Condition(this);

            if (!enabled)
                touch = default(TouchLocation);

            TouchPrev = Touch;
            Touch = touch;

            alphaTime = Calc.Clamp(alphaTime + Engine.RawDeltaTime * (enabled ? 1f : -1f), 0f, 0.3f);
            alpha = Ease.CubeOut(alphaTime / 0.3f);

            colorTime = Calc.Clamp(colorTime + Engine.RawDeltaTime * (touch.State != TouchLocationState.Invalid ? 1f : -1f), 0f, 0.1f);
            int colorMulDown = (int) Math.Round(colorTime / 0.1f * 255f);
            int colorMulUp = 255 - colorMulDown;
            color = new Color(
                ((ColorUp.R * colorMulUp + ColorDown.R * colorMulDown) / 2) / 65025,
                ((ColorUp.G * colorMulUp + ColorDown.G * colorMulDown) / 2) / 65025,
                ((ColorUp.B * colorMulUp + ColorDown.B * colorMulDown) / 2) / 65025,
                ((ColorUp.A * colorMulUp + ColorDown.A * colorMulDown) / 2) / 65025
            );

            bool consumed = false;
            if (touch.State != TouchLocationState.Invalid)
                consumed = OnTouch?.InvokeWhileFalse(this, touch) ?? true;
            return consumed;
        }

        public override void Draw() {
            if (Icon == null)
                return;

            Icon.DrawJustified(
                Position,
                Vector2.One * 0.5f,
                color * alpha,
                new Vector2(
                    Size.X / Icon.Width,
                    Size.Y / Icon.Height
                )
            );
        }

    }
}
