using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost {
    public struct GhostFrame {

        public void Read(BinaryReader reader) {
            string chunk;
            // The last "chunk" type, \r\n (Windows linebreak), doesn't contain a length.
            while ((chunk = reader.ReadNullTerminatedString()) != "\r\n") {
                uint length = reader.ReadUInt32();
                switch (chunk) {
                    case "data":
                        ReadChunkData(reader);
                        break;
                    default:
                        // Skip any unknown chunks.
                        reader.BaseStream.Seek(length, SeekOrigin.Current);
                        break;
                }
            }
        }

        public void Write(BinaryWriter writer) {
            WriteChunkData(writer);

            writer.WriteNullTerminatedString("\r\n");
        }

        public long WriteChunkStart(BinaryWriter writer, string name) {
            writer.WriteNullTerminatedString(name);
            writer.Write(0U); // Filled in later.
            long start = writer.BaseStream.Position;
            return start;
        }

        public void WriteChunkEnd(BinaryWriter writer, long start) {
            long pos = writer.BaseStream.Position;
            long length = pos - start;

            // Update the chunk length, which consists of the 4 bytes before the chunk data.
            writer.Flush();
            writer.BaseStream.Seek(start - 4, SeekOrigin.Begin);
            writer.Write((int) length);

            writer.Flush();
            writer.BaseStream.Seek(pos, SeekOrigin.Begin);
        }

        public bool HasData;

        public bool InControl;

        public Vector2 Position;
        public Vector2 Speed;
        public float Rotation;
        public Vector2 Scale;
        public Color Color;

        public float SpriteRate;
        public Vector2? SpriteJustify;

        public Facings Facing;

        public string CurrentAnimationID;
        public int CurrentAnimationFrame;

        public Color HairColor;
        public bool HairSimulateMotion;

        public void ReadChunkData(BinaryReader reader) {
            HasData = true;

            InControl = reader.ReadBoolean();

            Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Speed = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Rotation = reader.ReadSingle();
            Scale = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Color = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());

            SpriteRate = reader.ReadSingle();
            SpriteJustify = reader.ReadBoolean() ? (Vector2?) new Vector2(reader.ReadSingle(), reader.ReadSingle()) : null;

            Facing = (Facings) reader.ReadInt32();

            CurrentAnimationID = reader.ReadNullTerminatedString();
            CurrentAnimationFrame = reader.ReadInt32();

            HairColor = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            HairSimulateMotion = reader.ReadBoolean();
        }

        public void WriteChunkData(BinaryWriter writer) {
            if (!HasData)
                return;
            long start = WriteChunkStart(writer, "data");

            writer.Write(InControl);

            writer.Write(Position.X);
            writer.Write(Position.Y);

            writer.Write(Speed.X);
            writer.Write(Speed.Y);

            writer.Write(Rotation);

            writer.Write(Scale.X);
            writer.Write(Scale.Y);

            writer.Write(Color.R);
            writer.Write(Color.G);
            writer.Write(Color.B);
            writer.Write(Color.A);

            writer.Write(SpriteRate);

            if (SpriteJustify != null) {
                writer.Write(true);
                writer.Write(SpriteJustify.Value.X);
                writer.Write(SpriteJustify.Value.Y);
            } else {
                writer.Write(false);
            }

            writer.Write((int) Facing);

            writer.WriteNullTerminatedString(CurrentAnimationID);
            writer.Write(CurrentAnimationFrame);

            writer.Write(HairColor.R);
            writer.Write(HairColor.G);
            writer.Write(HairColor.B);
            writer.Write(HairColor.A);

            writer.Write(HairSimulateMotion);

            WriteChunkEnd(writer, start);
        }

    }
}
