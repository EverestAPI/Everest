using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost {
    public struct GhostFrame {

        public bool Valid;

        public Vector2 Position;
        public Vector2 Speed;
        public float Rotation;
        public Vector2 Scale;
        public Color Color;

        public Facings Facing;

        public string CurrentAnimationID;
        public int CurrentAnimationFrame;

        public Color HairColor;
        public bool HairSimulateMotion;

    }
}
