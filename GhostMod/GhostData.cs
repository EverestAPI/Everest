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
using System.Text.RegularExpressions;

namespace Celeste.Mod.Ghost {
    public class GhostData {

        public readonly static string Magic = "everest-ghost\r\n";
        public readonly static char[] MagicChars = Magic.ToCharArray();

        public readonly static int Version = 0;

        public readonly static Regex PathVerifyRegex = new Regex("[\"`" + Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars())) + "]", RegexOptions.Compiled);

        public static string GetPath(Session session, int transition)
            => Path.Combine(
                Everest.PathSettings, "Ghosts",
                PathVerifyRegex.Replace(
                    session.Area.GetSID().Replace('/', '-') + "-" + session.Area.Mode.ToString() + "-" + transition + "-" + session.Level + ".oshiro",
                    "_"
                )
            );

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
                // Keep existing frames in-tact.
                return null;

            if (!File.Exists(FilePath)) {
                // File doesn't exist - load nothing.
                Logger.Log("ghost", $"Ghost doesn't exist: {FilePath}");
                Frames = new List<GhostFrame>();
                return null;
            }

            using (Stream stream = File.OpenRead(FilePath))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                return Read(reader);
        }
        public GhostData Read(BinaryReader reader) {
            if (reader.ReadInt16() != 0x0ade)
                return null; // Endianness mismatch.

            char[] magic = reader.ReadChars(MagicChars.Length);
            if (magic.Length != MagicChars.Length)
                return null; // Didn't read as much as we wanted to read.
            for (int i = 0; i < MagicChars.Length; i++) {
                if (magic[i] != MagicChars[i])
                    return null; // Magic mismatch.
            }

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
            writer.Write((short) 0x0ade);
            writer.Write(MagicChars);
            writer.Write(Version);
            writer.Write(Frames.Count);
            for (int i = 0; i < Frames.Count; i++) {
                GhostFrame frame = Frames[i];
                frame.Write(writer);
            }
        }

    }
}
