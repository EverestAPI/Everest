using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Meta {
    // MapMeta and anything related doesn't need interfaces.
    public class MapMeta : IMeta {

        public MapMeta() {
        }

        public MapMeta(BinaryPacker.Element meta) {
            Parse(meta);
        }

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

        public Session.CoreModes CoreMode { get; set; }

        public string CassetteNoteColor { get; set; } = null;
        public string CassetteSong { get; set; } = null;

        public MapMetaModeProperties[] Modes { get; set; } = null;

        public MapMetaMountain Mountain { get; set; } = null;

        public MapMetaCompleteScreen CompleteScreen { get; set; } = null;

        public MapMetaCassetteModifier CassetteModifier { get; set; } = null;

        public bool FixRotateSpinnerAngle { get; set; } = true;

        public void Parse(BinaryPacker.Element meta) {
            meta.AttrIf("Name", v => Name = v);
            meta.AttrIf("SID", v => SID = v);
            meta.AttrIf("Icon", v => Icon = v);

            meta.AttrIfBool("Interlude", v => Interlude = v);
            meta.AttrIf("CompleteScreenName", v => CompleteScreenName = v);

            meta.AttrIfInt("CassetteCheckpointIndex", v => CassetteCheckpointIndex = v);

            meta.AttrIf("TitleBaseColor", v => TitleBaseColor = v);
            meta.AttrIf("TitleAccentColor", v => TitleAccentColor = v);
            meta.AttrIf("TitleTextColor", v => TitleTextColor = v);

            meta.AttrIf("IntroType", v => IntroType = (Player.IntroTypes) Enum.Parse(typeof(Player.IntroTypes), v, true));

            meta.AttrIfBool("Dreaming", v => Dreaming = v);

            meta.AttrIf("ColorGrade", v => ColorGrade = v);

            meta.AttrIf("Wipe", v => Wipe = v);

            meta.AttrIfFloat("DarknessAlpha", (float v) => DarknessAlpha = v);
            meta.AttrIfFloat("BloomBase", (float v) => BloomBase = v);
            meta.AttrIfFloat("BloomStrength", (float v) => BloomStrength = v);

            meta.AttrIf("Jumpthru", v => Jumpthru = v);

            meta.AttrIf("CoreMode", v => CoreMode = (Session.CoreModes) Enum.Parse(typeof(Session.CoreModes), v, true));

            meta.AttrIf("CassetteNoteColor", v => CassetteNoteColor = v);
            meta.AttrIf("CassetteSong", v => CassetteSong = v);

            meta.AttrIfBool("FixRotateSpinnerAngles", v => FixRotateSpinnerAngle = v);

            BinaryPacker.Element child;

            child = meta.Children.FirstOrDefault(el => el.Name == "cassettemodifier");
            if (child != null)
                CassetteModifier = new MapMetaCassetteModifier(child);

            Modes = new MapMetaModeProperties[3];
            child = meta.Children.FirstOrDefault(el => el.Name == "modes");
            if (child != null) {
                for (int i = 0; i < child.Children.Count; i++) {
                    Modes[i] = new MapMetaModeProperties(child.Children[i]);
                }
                for (int i = child.Children.Count; i < Modes.Length; i++) {
                    Modes[i] = null;
                }
            }
        }

        public void ApplyTo(AreaData area) {
            if (!string.IsNullOrEmpty(Name))
                area.Name = Name;

            if (!string.IsNullOrEmpty(SID))
                area.SetSID(SID);

            if (!string.IsNullOrEmpty(Icon) && GFX.Gui.Has(Icon))
                area.Icon = Icon;

            area.Interlude = Interlude;
            if (!string.IsNullOrEmpty(CompleteScreenName))
                area.CompleteScreenName = CompleteScreenName;

            area.CassetteCheckpointIndex = CassetteCheckpointIndex;

            if (!string.IsNullOrEmpty(TitleBaseColor))
                area.TitleBaseColor = Calc.HexToColor(TitleBaseColor);
            if (!string.IsNullOrEmpty(TitleAccentColor))
                area.TitleAccentColor = Calc.HexToColor(TitleAccentColor);
            if (!string.IsNullOrEmpty(TitleTextColor))
                area.TitleTextColor = Calc.HexToColor(TitleTextColor);

            area.IntroType = IntroType;

            area.Dreaming = Dreaming;
            if (!string.IsNullOrEmpty(ColorGrade))
                area.ColorGrade = ColorGrade;

            ModeProperties[] modes = area.Mode;
            area.Mode = Convert(Modes) ?? modes;
            if (modes != null)
                for (int i = 0; i < area.Mode.Length && i < modes.Length; i++)
                    if (area.Mode[i] == null)
                        area.Mode[i] = modes[i];

            if (!string.IsNullOrEmpty(Wipe)) {
                Type type = Assembly.GetEntryAssembly().GetType(Wipe);
                ConstructorInfo ctor = type?.GetConstructor(new Type[] { typeof(Scene), typeof(bool), typeof(Action) });
                if (type != null && ctor != null) {
                    area.Wipe = (scene, wipeIn, onComplete) => ctor.Invoke(new object[] { scene, wipeIn, onComplete });
                }
            }

            area.DarknessAlpha = DarknessAlpha;
            area.BloomBase = BloomBase;
            area.BloomStrength = BloomStrength;

            if (!string.IsNullOrEmpty(Jumpthru))
                area.Jumpthru = Jumpthru;

            area.CoreMode = CoreMode;

            if (!string.IsNullOrEmpty(CassetteNoteColor))
                area.CassseteNoteColor = Calc.HexToColor(CassetteNoteColor);
            if (!string.IsNullOrEmpty(CassetteSong))
                area.CassetteSong = CassetteSong;

            area.MountainIdle = Mountain?.Idle?.Convert() ?? area.MountainIdle;
            area.MountainSelect = Mountain?.Select?.Convert() ?? area.MountainSelect;
            area.MountainZoom = Mountain?.Zoom?.Convert() ?? area.MountainZoom;
            area.MountainCursor = Mountain?.Cursor?.ToVector3() ?? area.MountainCursor;
            area.MountainState = Mountain?.State ?? area.MountainState;

            area.SetMeta(this);
        }

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
        public MapMetaModeProperties() {
        }

        public MapMetaModeProperties(BinaryPacker.Element meta) {
            Parse(meta);
        }

        public MapMetaAudioState AudioState { get; set; }
        public MapMetaCheckpointData[] Checkpoints { get; set; }
        public bool? IgnoreLevelAudioLayerData { get; set; }
        public string Inventory { get; set; }
        public string Path { get; set; }
        public string PoemID { get; set; }

        public string StartLevel { get; set; }
        public bool? HeartIsEnd { get; set; }

        public ModeProperties Convert()
            => new ModeProperties() {
                AudioState = AudioState?.Convert() ?? new AudioState(Sfxs.music_city, Sfxs.env_amb_01_main),
                Checkpoints = MapMeta.Convert(Checkpoints), // Can be null.
                IgnoreLevelAudioLayerData = IgnoreLevelAudioLayerData ?? false,
                Inventory = MapMeta.GetInventory(Inventory) ?? PlayerInventory.Default,
                Path = Path,
                PoemID = PoemID
            };

        public void Parse(BinaryPacker.Element meta) {
            meta.AttrIfBool("IgnoreLevelAudioLayerData", v => IgnoreLevelAudioLayerData = v);
            meta.AttrIf("Inventory", v => Inventory = v);
            meta.AttrIf("Path", v => Path = v);
            meta.AttrIf("PoemID", v => PoemID = v);
            meta.AttrIf("StartLevel", v => StartLevel = v);
            meta.AttrIf("HeartIsEnd", v => HeartIsEnd = string.IsNullOrEmpty(v) ? (bool?) null : (bool) bool.Parse(v));

            BinaryPacker.Element child;

            child = meta.Children.FirstOrDefault(el => el.Name == "audiostate");
            if (child != null)
                AudioState = new MapMetaAudioState(child);

            child = meta.Children.FirstOrDefault(el => el.Name == "checkpoints");
            if (child != null) {
                Checkpoints = new MapMetaCheckpointData[child.Children.Count];
                for (int i = 0; i < Checkpoints.Length; i++) {
                    Checkpoints[i] = new MapMetaCheckpointData(child.Children[i]);
                }
            }
        }

        public void ApplyTo(AreaData area, AreaMode mode) {
            area.GetMeta().Modes[(int) mode] = this;
            ModeProperties props = area.Mode[(int) mode];
            if (props != null) {
                props.AudioState = AudioState?.Convert() ?? props.AudioState;
                props.Checkpoints = MapMeta.Convert(Checkpoints) ?? props.Checkpoints;
                props.IgnoreLevelAudioLayerData = IgnoreLevelAudioLayerData ?? props.IgnoreLevelAudioLayerData;
                props.Inventory = MapMeta.GetInventory(Inventory) ?? props.Inventory;
                props.Path = Path ?? props.Path;
                props.PoemID = PoemID ?? props.PoemID;
            } else {
                props = Convert();
            }
            area.Mode[(int) mode] = props;
        }
    }
    public class MapMetaAudioState {
        public MapMetaAudioState() {
        }

        public MapMetaAudioState(BinaryPacker.Element meta) {
            Parse(meta);
        }

        public string Music { get; set; }
        public string Ambience { get; set; }
        public AudioState Convert()
            => new AudioState(Music, Ambience);

        public void Parse(BinaryPacker.Element meta) {
            meta.AttrIf("Music", v => Music = v);
            meta.AttrIf("Ambience", v => Ambience = v);
        }
    }
    public class MapMetaCheckpointData {
        public MapMetaCheckpointData() {
        }

        public MapMetaCheckpointData(BinaryPacker.Element meta) {
            Parse(meta);
        }

        public string Level { get; set; }
        public string Name { get; set; }
        public bool Dreaming { get; set; }
        public string Inventory { get; set; }
        public MapMetaAudioState AudioState { get; set; }
        public string[] Flags { get; set; }
        public Session.CoreModes? CoreMode { get; set; }
        public CheckpointData Convert()
            => new CheckpointData(Level, Name, MapMeta.GetInventory(Inventory), Dreaming, AudioState?.Convert()) {
                Flags = new HashSet<string>(Flags ?? new string[0]),
                CoreMode = CoreMode
            };

        public void Parse(BinaryPacker.Element meta) {
            meta.AttrIf("Level", v => Level = v);
            meta.AttrIf("Name", v => Name = v);
            meta.AttrIfBool("Dreaming", v => Dreaming = v);
            meta.AttrIf("Inventory", v => Inventory = v);
            meta.AttrIf("CoreMode", v => CoreMode = string.IsNullOrEmpty(v) ? (Session.CoreModes?) null : (Session.CoreModes) Enum.Parse(typeof(Session.CoreModes), v, true));

            BinaryPacker.Element child;

            child = meta.Children.FirstOrDefault(el => el.Name == "audiostate");
            if (child != null)
                AudioState = new MapMetaAudioState(child);

            child = meta.Children.FirstOrDefault(el => el.Name == "flags");
            if (child != null) {
                Flags = new string[child.Children.Count];
                for (int i = 0; i < Flags.Length; i++) {
                    Flags[i] = child.Children[i].Attr("innerText");
                }
            }
        }
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

    public class MapMetaCompleteScreen {
        public string Atlas { get; set; }
        [YamlIgnore] public Vector2 Start => StartArray.ToVector2() ?? Vector2.Zero;
        [YamlMember(Alias = "Start")] public float[] StartArray { get; set; }
        [YamlIgnore] public Vector2 Center => CenterArray.ToVector2() ?? Vector2.Zero;
        [YamlMember(Alias = "Center")] public float[] CenterArray { get; set; }
        [YamlIgnore] public Vector2 Offset => OffsetArray.ToVector2() ?? Vector2.Zero;
        [YamlMember(Alias = "Offset")] public float[] OffsetArray { get; set; }
        public MapMetaCompleteScreenLayer[] Layers { get; set; }
    }
    public class MapMetaCompleteScreenLayer {
        public string Type { get; set; }
        public string[] Images { get; set; }
        [YamlIgnore] public Vector2 Position => PositionArray.ToVector2() ?? Vector2.Zero;
        [YamlMember(Alias = "Position")] public float[] PositionArray { get; set; }
        [YamlIgnore] public Vector2 Scroll => ScrollArray.ToVector2() ?? Vector2.Zero;
        [YamlMember(Alias = "Scroll")] public float[] ScrollArray { get; set; }
        public float FrameRate { get; set; } = 6f;
        public float Alpha { get; set; } = 1f;
        [YamlIgnore] public Vector2 Speed => SpeedArray.ToVector2() ?? Vector2.Zero;
        [YamlMember(Alias = "Speed")] public float[] SpeedArray { get; set; }
    }

    public class MapMetaCassetteModifier {
        public MapMetaCassetteModifier() {
        }

        public MapMetaCassetteModifier(BinaryPacker.Element meta) {
            Parse(meta);
        }

        public float TempoMult { get; set; } = 1f;
        public int LeadBeats { get; set; } = 16;
        public int BeatsPerTick { get; set; } = 4;
        public int TicksPerSwap { get; set; } = 2;
        public int Blocks { get; set; } = 2;
        public int BeatsMax { get; set; } = 256;

        public void Parse(BinaryPacker.Element meta) {
            meta.AttrIfFloat("TempoMult", v => TempoMult = v);
            meta.AttrIfInt("LeadBeats", v => LeadBeats = v);
            meta.AttrIfInt("BeatsPerTick", v => BeatsPerTick = v);
            meta.AttrIfInt("TicksPerSwap", v => TicksPerSwap = v);
            meta.AttrIfInt("Blocks", v => Blocks = v);
            meta.AttrIfInt("BeatsMax", v => BeatsMax = v);
        }
    }
}
