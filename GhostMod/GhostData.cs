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

        public readonly static string Magic = "\u0ade-everest-ghost\r\n";

        public readonly static int Version = 0;

        public static string GetPath(Session session, int transition)
            => Path.Combine(
                Everest.PathSettings, "Ghosts",
                session.Area.GetSID().Replace('/', Path.DirectorySeparatorChar),
                session.Area.Mode.ToString(),
                transition + "-" + session.Level + "-" + SpawnToPath(session.RespawnPoint) + ".oshiro"
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
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
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
                frame.Read(reader);
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
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                Write(writer);
        }
        public void Write(BinaryWriter writer) {
            writer.Write(Version);
            writer.Write(Frames.Count);
            for (int i = 0; i < Frames.Count; i++) {
                GhostFrame frame = Frames[i];

                frame.Write(writer);
            }
        }

    }
}
