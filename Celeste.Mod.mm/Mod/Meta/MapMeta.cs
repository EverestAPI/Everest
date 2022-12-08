using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Meta {
    // MapMeta and anything related doesn't need interfaces.
    public class MapMeta : IMeta {

        public MapMeta() {
        }

        public MapMeta(BinaryPacker.Element meta) {
            Parse(meta);
        }

        // The following properties have been banned as they have been heavily misused in the past.
        // public string Name { get; set; }
        // public string CompleteScreenName { get; set; }
        // public string SID { get; set; }

        public string Parent { get; set; }

        public string Icon { get; set; }

        public bool? Interlude { get; set; }

        public int? CassetteCheckpointIndex { get; set; }

        public string TitleBaseColor { get; set; }
        public string TitleAccentColor { get; set; }
        public string TitleTextColor { get; set; }

        public Player.IntroTypes? IntroType { get; set; }

        public bool? Dreaming { get; set; } = false;

        public string ColorGrade { get; set; }

        public string Wipe { get; set; }

        public float? DarknessAlpha { get; set; }
        public float? BloomBase { get; set; }
        public float? BloomStrength { get; set; }

        public string Jumpthru { get; set; }

        public Session.CoreModes? CoreMode { get; set; }

        public string CassetteNoteColor { get; set; }
        public string CassetteSong { get; set; }

        public string PostcardSoundID { get; set; }

        public string ForegroundTiles { get; set; }
        public string BackgroundTiles { get; set; }
        public string AnimatedTiles { get; set; }
        public string Sprites { get; set; }
        public string Portraits { get; set; }

        public bool? OverrideASideMeta { get; set; }

        public MapMetaModeProperties[] Modes { get; set; }

        public MapMetaMountain Mountain { get; set; }

        public MapMetaCompleteScreen CompleteScreen { get; set; }

        public MapMetaCompleteScreen LoadingVignetteScreen { get; set; }

        public MapMetaTextVignette LoadingVignetteText { get; set; }

        public MapMetaCassetteModifier CassetteModifier { get; set; }

        public void Parse(BinaryPacker.Element meta) {
            meta.AttrIf("Parent", v => Parent = v);

            meta.AttrIf("Icon", v => Icon = v);

            meta.AttrIfBool("Interlude", v => Interlude = v);

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

            meta.AttrIf("PostcardSoundID", v => PostcardSoundID = v);

            meta.AttrIf("ForegroundTiles", v => ForegroundTiles = v);
            meta.AttrIf("BackgroundTiles", v => BackgroundTiles = v);
            meta.AttrIf("AnimatedTiles", v => AnimatedTiles = v);
            meta.AttrIf("Sprites", v => Sprites = v);
            meta.AttrIf("Portraits", v => Portraits = v);
            meta.AttrIfBool("OverrideASideMeta", v => OverrideASideMeta = v);

            BinaryPacker.Element child;

            child = meta.Children?.FirstOrDefault(el => el.Name == "cassettemodifier");
            if (child != null)
                CassetteModifier = new MapMetaCassetteModifier(child);

            Modes = new MapMetaModeProperties[3];
            child = meta.Children?.FirstOrDefault(el => el.Name == "modes");
            if (child != null && child.Children != null) {
                for (int i = 0; i < child.Children.Count; i++) {
                    Modes[i] = new MapMetaModeProperties(child.Children[i]);
                }
                for (int i = child.Children?.Count ?? 0; i < Modes.Length; i++) {
                    Modes[i] = null;
                }
            }
        }

        public void ApplyTo(AreaData area) {
            if (!string.IsNullOrEmpty(Icon) && GFX.Gui.Has(Icon))
                area.Icon = Icon;

            if (Interlude != null)
                area.Interlude = Interlude.Value;

            if (CassetteCheckpointIndex != null)
                area.CassetteCheckpointIndex = CassetteCheckpointIndex.Value;

            if (!string.IsNullOrEmpty(TitleBaseColor))
                area.TitleBaseColor = Calc.HexToColor(TitleBaseColor);
            if (!string.IsNullOrEmpty(TitleAccentColor))
                area.TitleAccentColor = Calc.HexToColor(TitleAccentColor);
            if (!string.IsNullOrEmpty(TitleTextColor))
                area.TitleTextColor = Calc.HexToColor(TitleTextColor);

            if (IntroType != null)
                area.IntroType = IntroType.Value;

            if (Dreaming != null)
                area.Dreaming = Dreaming.Value;

            if (!string.IsNullOrEmpty(ColorGrade))
                area.ColorGrade = ColorGrade;

            if (!string.IsNullOrEmpty(Wipe)) {
                Type type = Assembly.GetEntryAssembly().GetType(Wipe);
                ConstructorInfo ctor = type?.GetConstructor(new Type[] { typeof(Scene), typeof(bool), typeof(Action) });
                if (type != null && ctor != null) {
                    area.Wipe = (scene, wipeIn, onComplete) => ctor.Invoke(new object[] { scene, wipeIn, onComplete });
                }
            }

            if (DarknessAlpha != null)
                area.DarknessAlpha = DarknessAlpha.Value;
            if (BloomBase != null)
                area.BloomBase = BloomBase.Value;
            if (BloomStrength != null)
                area.BloomStrength = BloomStrength.Value;

            if (!string.IsNullOrEmpty(Jumpthru))
                area.Jumpthru = Jumpthru;

            if (CoreMode != null)
                area.CoreMode = CoreMode.Value;

            if (!string.IsNullOrEmpty(CassetteNoteColor))
                area.CassseteNoteColor = Calc.HexToColor(CassetteNoteColor);
            if (!string.IsNullOrEmpty(CassetteSong))
                area.CassetteSong = CassetteSong;

            area.MountainIdle = Mountain?.Idle?.Convert() ?? area.MountainIdle;
            area.MountainSelect = Mountain?.Select?.Convert() ?? area.MountainSelect;
            area.MountainZoom = Mountain?.Zoom?.Convert() ?? area.MountainZoom;
            area.MountainCursor = Mountain?.Cursor?.ToVector3() ?? area.MountainCursor;
            area.MountainState = Mountain?.State ?? area.MountainState;

            ModeProperties[] modes = area.Mode;
            area.Mode = Convert(Modes) ?? modes;
            if (modes != null)
                for (int i = 0; i < area.Mode.Length && i < modes.Length; i++)
                    if (area.Mode[i] == null)
                        area.Mode[i] = modes[i];

            MapMeta meta = area.GetMeta();
            if (meta == null) {
                area.SetMeta(this);
            } else {
                if (!string.IsNullOrEmpty(Parent))
                    meta.Parent = Parent;

                if (!string.IsNullOrEmpty(PostcardSoundID))
                    meta.PostcardSoundID = PostcardSoundID;

                if (!string.IsNullOrEmpty(ForegroundTiles))
                    meta.ForegroundTiles = ForegroundTiles;

                if (!string.IsNullOrEmpty(BackgroundTiles))
                    meta.BackgroundTiles = BackgroundTiles;

                if (!string.IsNullOrEmpty(AnimatedTiles))
                    meta.AnimatedTiles = AnimatedTiles;

                if (!string.IsNullOrEmpty(Sprites))
                    meta.Sprites = Sprites;

                if (!string.IsNullOrEmpty(Portraits))
                    meta.Portraits = Portraits;

                if (OverrideASideMeta != null)
                    meta.OverrideASideMeta = OverrideASideMeta;

                if ((Modes?.Length ?? 0) != 0 && Modes.Any(mode => mode != null))
                    meta.Modes = Modes;

                if (Mountain != null)
                    meta.Mountain = Mountain;

                if (CompleteScreen != null)
                    meta.CompleteScreen = CompleteScreen;

                if (LoadingVignetteScreen != null)
                    meta.LoadingVignetteScreen = LoadingVignetteScreen;

                if (LoadingVignetteText != null)
                    meta.LoadingVignetteText = LoadingVignetteText;

                if (CassetteModifier != null)
                    meta.CassetteModifier = CassetteModifier;
            }
        }

        public void ApplyToForOverride(AreaData area) {
            if (IntroType != null)
                area.IntroType = IntroType.Value;

            if (Dreaming != null)
                area.Dreaming = Dreaming.Value;

            if (!string.IsNullOrEmpty(ColorGrade))
                area.ColorGrade = ColorGrade;

            if (DarknessAlpha != null)
                area.DarknessAlpha = DarknessAlpha.Value;

            if (BloomBase != null)
                area.BloomBase = BloomBase.Value;

            if (BloomStrength != null)
                area.BloomStrength = BloomStrength.Value;

            if (CoreMode != null)
                area.CoreMode = CoreMode.Value;
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
                case "Farewell":
                    return PlayerInventory.Farewell;
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
        public bool? SeekerSlowdown { get; set; }
        public bool? TheoInBubble { get; set; }

        public ModeProperties Convert()
            => new ModeProperties() {
                AudioState = AudioState?.Convert() ?? new AudioState(SFX.music_city, SFX.env_amb_01_main),
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
            meta.AttrIfBool("HeartIsEnd", v => HeartIsEnd = v);
            meta.AttrIfBool("SeekerSlowdown", v => SeekerSlowdown = v);
            meta.AttrIfBool("TheoInBubble", v => TheoInBubble = v);

            BinaryPacker.Element child;

            child = meta.Children?.FirstOrDefault(el => el.Name == "audiostate");
            if (child != null)
                AudioState = new MapMetaAudioState(child);

            child = meta.Children?.FirstOrDefault(el => el.Name == "checkpoints");
            if (child != null) {
                Checkpoints = new MapMetaCheckpointData[child.Children?.Count ?? 0];
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
            meta.AttrIf("CoreMode", v => CoreMode = (Session.CoreModes) Enum.Parse(typeof(Session.CoreModes), v, true));

            BinaryPacker.Element child;

            child = meta.Children?.FirstOrDefault(el => el.Name == "audiostate");
            if (child != null)
                AudioState = new MapMetaAudioState(child);

            child = meta.Children?.FirstOrDefault(el => el.Name == "flags");
            if (child != null) {
                Flags = new string[child.Children?.Count ?? 0];
                for (int i = 0; i < Flags.Length; i++) {
                    Flags[i] = child.Children[i].Attr("innerText");
                }
            }
        }
    }
    public class MapMetaMountain {
        public string MountainModelDirectory { get; set; } = null;
        public string MountainTextureDirectory { get; set; } = null;
        public string BackgroundMusic { get; set; } = null;
        public string BackgroundAmbience { get; set; } = null;
        public Dictionary<string, float> BackgroundMusicParams { get; set; } = null;
        public string[] FogColors { get; set; } = null;
        public string StarFogColor { get; set; } = null;
        public string[] StarStreamColors { get; set; } = null;
        public string[] StarBeltColors1 { get; set; } = null;
        public string[] StarBeltColors2 { get; set; } = null;
        public MapMetaMountainCamera Idle { get; set; } = null;
        public MapMetaMountainCamera Select { get; set; } = null;
        public MapMetaMountainCamera Zoom { get; set; } = null;
        public float[] Cursor { get; set; } = null;
        public int State { get; set; } = 0;
        public bool Rotate { get; set; } = false;
        public bool ShowCore { get; set; } = false;
        public bool ShowSnow { get; set; } = true;

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

        public string[] MusicBySide { get; set; }

        public MapMetaCompleteScreenTitle Title { get; set; }
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
        public float Scale { get; set; } = 1f;
        public bool Loop { get; set; } = true;
    }

    public class MapMetaCompleteScreenTitle {
        public string ASide { get; set; }
        public string BSide { get; set; }
        public string CSide { get; set; }
        public string FullClear { get; set; }
    }

    public class MapMetaTextVignette {
        public string Dialog { get; set; }
        public string Audio { get; set; } = SFX.music_prologue_intro_vignette; // for backwards compatibility reasons, default to prologue audio if not specified
        public float InitialDelay { get; set; } = 3;
        public float FinalDelay { get; set; }
        [YamlIgnore] public Vector2 SnowDirection => SnowDirectionArray.ToVector2() ?? -Vector2.UnitX; //Snowing to the left by default
        [YamlMember(Alias = "SnowDirection")] public float[] SnowDirectionArray { get; set; }
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
        public int BeatIndexOffset { get; set; } = 0;
        public bool OldBehavior { get; set; } = false;

        public void Parse(BinaryPacker.Element meta) {
            meta.AttrIfFloat("TempoMult", v => TempoMult = v);
            meta.AttrIfInt("LeadBeats", v => LeadBeats = v);
            meta.AttrIfInt("BeatsPerTick", v => BeatsPerTick = v);
            meta.AttrIfInt("TicksPerSwap", v => TicksPerSwap = v);
            meta.AttrIfInt("Blocks", v => Blocks = v);
            meta.AttrIfInt("BeatsMax", v => BeatsMax = v);
            meta.AttrIfInt("BeatIndexOffset", v => BeatIndexOffset = v);
            meta.AttrIfBool("OldBehavior", v => OldBehavior = v);
        }
    }
}
