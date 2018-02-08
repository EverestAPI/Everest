using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Meta {
    // MapMeta and anything related doesn't need interfaces.
    public class MapMeta : IMeta {

        public string Name { get; set; } = null;
        public string SID { get; set; } = null;
        public string Icon { get; set; } = null;

        public bool Interlude { get; set; } = false;
        public string CompleteScreenName { get; set; } = null;

        public int CassetteCheckpointIndex { get; set; } = 0;

        public string TitleBaseColor { get; set; } = null;
        public string TitleAccentColor { get; set; } = null;
        public string TitleTextColor { get; set; } = null;

        public Player.IntroTypes IntroType { get; set; } = Player.IntroTypes.WakeUp;

        public bool Dreaming { get; set; } = false;

        public string ColorGrade { get; set; } = null;

        public string Wipe { get; set; } = null;

        public float DarknessAlpha { get; set; } = 0.05f;
        public float BloomBase { get; set; } = 0f;
        public float BloomStrength { get; set; } = 1f;

        public string Jumpthru { get; set; } = null;

        public string CassetteNoteColor { get; set; } = null;
        public string CassetteSong { get; set; } = null;

        public MapMetaModeProperties[] Modes { get; set; } = null;

        public MapMetaMountain Mountain { get; set; } = null;

        public static ModeProperties[] Convert(MapMetaModeProperties[] meta) {
            if (meta == null || meta.Length == 0)
                return null;
            ModeProperties[] data = new ModeProperties[meta.Length];
            for (int i = 0; i < meta.Length; i++)
                data[i] = meta[i]?.Convert();
            return data;
        }

        public static CheckpointData[] Convert(MapMetaCheckpointData[] meta) {
            if (meta == null || meta.Length == 0)
                return null;
            CheckpointData[] data = new CheckpointData[meta.Length];
            for (int i = 0; i < meta.Length; i++)
                data[i] = meta[i]?.Convert();
            return data;
        }

        public static PlayerInventory? GetInventory(string meta) {
            if (string.IsNullOrEmpty(meta))
                return null;
            // TODO: Allow mod player inventories in the future.
            switch (meta) {
                case "Default":
                    return PlayerInventory.Default;
                case "CH6End":
                    return PlayerInventory.CH6End;
                case "Core":
                    return PlayerInventory.Core;
                case "OldSite":
                    return PlayerInventory.OldSite;
                case "Prologue":
                    return PlayerInventory.Prologue;
                case "TheSummit":
                    return PlayerInventory.TheSummit;
            }
            return null;
        }

    }
    public class MapMetaModeProperties {
        public MapMetaAudioState AudioState { get; set; }
        public MapMetaCheckpointData[] Checkpoints { get; set; }
        public bool IgnoreLevelAudioLayerData { get; set; }
        public string Inventory { get; set; }
        public string Path { get; set; }
        public string PoemID { get; set; }
        public ModeProperties Convert()
            => new ModeProperties() {
                AudioState = AudioState?.Convert() ?? new AudioState("event:/music/lvl1/main", "event:/env/amb/01_main"),
                Checkpoints = MapMeta.Convert(Checkpoints) ?? new CheckpointData[0],
                IgnoreLevelAudioLayerData = IgnoreLevelAudioLayerData,
                Inventory = MapMeta.GetInventory(Inventory) ?? PlayerInventory.Default,
                Path = Path,
                PoemID = PoemID
            };
    }
    public class MapMetaAudioState {
        public string Music { get; set; }
        public string Ambience { get; set; }
        public AudioState Convert()
            => new AudioState(Music, Ambience);
    }
    public class MapMetaCheckpointData {
        public string Level { get; set; }
        public string Name { get; set; }
        public bool Dreaming { get; set; }
        public int Strawberries { get; set; }
        public string Inventory { get; set; }
        public MapMetaAudioState AudioState { get; set; }
        public string[] Flags { get; set; }
        public Session.CoreModes? CoreMode { get; set; }
        public CheckpointData Convert()
            => new CheckpointData(Level, Name, MapMeta.GetInventory(Inventory), Dreaming, AudioState?.Convert());
    }
    public class MapMetaMountain {
        public MapMetaMountainCamera Idle { get; set; } = null;
        public MapMetaMountainCamera Select { get; set; } = null;
        public MapMetaMountainCamera Zoom { get; set; } = null;
        public float[] Cursor { get; set; } = null;
        public int State { get; set; } = 0;
    }
    public class MapMetaMountainCamera {
        public float[] Position { get; set; }
        public float[] Target { get; set; }
        public MountainCamera Convert()
            => new MountainCamera(Position?.ToVector3() ?? Vector3.Zero, Target?.ToVector3() ?? Vector3.Zero);
    }
}
