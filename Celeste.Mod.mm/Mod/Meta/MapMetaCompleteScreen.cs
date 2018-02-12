using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Meta {
    // MapMetaCompleteScreen and anything related doesn't need interfaces.
    public class MapMetaCompleteScreen : IMeta {

        public string Atlas { get; set; }

        // TODO: Vector2, Vector3, Vector4 <-> array (de)serializer.

        [YamlIgnore]
        public Vector2 Start { get; set; }
        [YamlMember(Alias = "Start")]
        public float[] StartArray {
            get {
                return new float[] { Start.X, Start.Y };
            }
            set {
                Start = new Vector2(value[0], value[1]);
            }
        }

        [YamlIgnore]
        public Vector2 Center { get; set; }
        [YamlMember(Alias = "Center")]
        public float[] CenterArray {
            get {
                return new float[] { Center.X, Center.Y };
            }
            set {
                Center = new Vector2(value[0], value[1]);
            }
        }

        [YamlIgnore]
        public Vector2 Offset { get; set; }
        [YamlMember(Alias = "Offset")]
        public float[] OffsetArray {
            get {
                return new float[] { Offset.X, Offset.Y };
            }
            set {
                Offset = new Vector2(value[0], value[1]);
            }
        }

        public MapMetaCompleteScreenLayer[] Layers { get; set; }

    }
    public class MapMetaCompleteScreenLayer {

        [YamlIgnore]
        public Vector2 Position { get; set; }
        [YamlMember(Alias = "Position")]
        public float[] PositionArray {
            get {
                return new float[] { Position.X, Position.Y };
            }
            set {
                Position = new Vector2(value[0], value[1]);
            }
        }

        [YamlIgnore]
        public Vector2 Scroll { get; set; }
        [YamlMember(Alias = "Scroll")]
        public float[] ScrollArray {
            get {
                if (Scroll.X == Scroll.Y)
                    return new float[] { Scroll.X };
                return new float[] { Scroll.X, Scroll.Y };
            }
            set {
                if (value.Length == 1) {
                    Scroll = new Vector2(value[0], value[0]);
                    return;
                }
                Scroll = new Vector2(value[0], value[1]);
            }
        }

        public string Type { get; set; }

        public string[] Images { get; set; }

        public float FrameRate { get; set; } = 6f;

        public float Alpha { get; set; } = 1f;

        [YamlIgnore]
        public Vector2 Speed { get; set; }
        [YamlMember(Alias = "Speed")]
        public float[] SpeedArray {
            get {
                return new float[] { Speed.X, Speed.Y };
            }
            set {
                Speed = new Vector2(value[0], value[1]);
            }
        }

    }
}
