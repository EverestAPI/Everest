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
using System.IO;

namespace Celeste.Mod.Ghost {
    public class GhostData {

        public readonly static int Version = 0;

        public static string GetPath(Session session, int transition)
            => Path.Combine(
                Everest.PathSettings, "Ghosts",
                session.Area.GetSID().Replace('/', Path.DirectorySeparatorChar),
                session.Area.Mode.ToString(),
                transition + "-" + session.Level + "-" + SpawnToPath(session.RespawnPoint) + ".bin"
            );
        private static string SpawnToPath(Vector2? point) {
            if (point == null)
                return "unknown";
            return ((int) Math.Round(point.Value.X)) + "x" + ((int) Math.Round(point.Value.Y));
        }

        public string FilePath;

        public List<GhostFrame> Frames = new List<GhostFrame>();

        public GhostFrame this[int i] {
            get {
                if (i < 0 || i >= Frames.Count)
                    return default(GhostFrame);
                return Frames[i];
            }
        }

        public GhostData()
            : this(null) {
        }
        public GhostData(Session session, int transition)
            : this(GetPath(session, transition)) {
        }
        public GhostData(string filePath) {
            FilePath = filePath;
        }

        public GhostData Read() {
            if (FilePath == null)
                return null;

            if (!File.Exists(FilePath)) {
                Frames = new List<GhostFrame>();
                return null;
            }

            using (Stream stream = File.OpenRead(FilePath))
            using (BinaryReader reader = new BinaryReader(stream))
                return Read(reader);
        }
        public GhostData Read(BinaryReader reader) {
            int version = reader.ReadInt32();
            // Don't read data from the future, but try to read data from the past.
            if (version > Version)
                return null;

            int count = reader.ReadInt32();
            Frames = new List<GhostFrame>(count);
            for (int i = 0; i < count; i++) {
                GhostFrame frame = new GhostFrame();
                frame.Valid = true;

                frame.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                frame.Speed = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                frame.Rotation = reader.ReadSingle();
                frame.Scale = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                frame.Color = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());

                frame.Facing = (Facings) reader.ReadInt32();

                frame.CurrentAnimationID = reader.ReadString();
                frame.CurrentAnimationFrame = reader.ReadInt32();

                frame.HairColor = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                frame.HairSimulateMotion = reader.ReadBoolean();

                Frames.Add(frame);
            }

            return this;
        }

        public void Write() {
            if (FilePath == null)
                return;
            if (File.Exists(FilePath))
                File.Delete(FilePath);

            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            using (Stream stream = File.OpenWrite(FilePath))
            using (BinaryWriter writer = new BinaryWriter(stream))
                Write(writer);
        }
        public void Write(BinaryWriter writer) {
            writer.Write(Version);
            writer.Write(Frames.Count);
            for (int i = 0; i < Frames.Count; i++) {
                GhostFrame frame = Frames[i];

                writer.Write(frame.Position.X);
                writer.Write(frame.Position.Y);

                writer.Write(frame.Speed.X);
                writer.Write(frame.Speed.Y);

                writer.Write(frame.Rotation);

                writer.Write(frame.Scale.X);
                writer.Write(frame.Scale.Y);

                writer.Write(frame.Color.R);
                writer.Write(frame.Color.G);
                writer.Write(frame.Color.B);
                writer.Write(frame.Color.A);

                writer.Write((int) frame.Facing);

                writer.Write(frame.CurrentAnimationID);
                writer.Write(frame.CurrentAnimationFrame);

                writer.Write(frame.HairColor.R);
                writer.Write(frame.HairColor.G);
                writer.Write(frame.HairColor.B);
                writer.Write(frame.HairColor.A);

                writer.Write(frame.HairSimulateMotion);
            }
        }

    }
}
