#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_AreaData : AreaData {

        public string SID;

        public string LevelSet {
            get {
                string sid = SID;
                if (string.IsNullOrEmpty(sid))
                    return "";
                int lastIndexOfSlash = sid.LastIndexOf('/');
                if (lastIndexOfSlash == -1)
                    return "";
                return sid.Substring(0, lastIndexOfSlash);
            }
        }

        [MonoModReplace]
        public static new AreaData Get(Scene scene) {
            AreaData result;
            if (scene != null && scene is Level) {
                result = Get(((Level) scene).Session.Area);
            } else {
                result = null;
            }
            return result;
        }

        [MonoModReplace]
        public static new AreaData Get(Session session) {
            AreaData result;
            if (session != null) {
                result = Get(session.Area);
            } else {
                result = null;
            }
            return result;
        }

        [MonoModReplace]
        public static new AreaData Get(AreaKey area) {
            return Get(area.GetSID()); // ?? Get(area.ID);
        }

        [MonoModReplace]
        public static new AreaData Get(int id) {
            return Areas[id];
        }

        public static AreaData Get(AreaStats stats) {
            return Get(stats.GetSID());
        }

        public static AreaData Get(string sid) {
            return Areas.Find(area => area.GetSID() == sid);
        }

        public static extern void orig_Load();
        public static new void Load() {
            orig_Load();

            foreach (AreaData area in Areas) {
                area.SetSID("Celeste/" + area.Mode[0].Path);
            }

            // Separate array as we sort it afterwards.
            List<AreaData> modAreas = new List<AreaData>();

            foreach (AssetMetadata asset in Everest.Content.ListMaps) {
                string path = asset.PathRelative.Substring(5);
                MapMeta meta = asset.GetMeta<MapMeta>();

                AreaData area = new AreaData();

                // Default values.

                area.SetSID(path);

                area.Name = path;
                area.Icon = "areas/" + path.ToLowerInvariant();
                if (!GFX.Gui.Has(area.Icon))
                    area.Icon = "areas/null";

                area.TitleBaseColor = Calc.HexToColor("6c7c81");
                area.TitleAccentColor = Calc.HexToColor("2f344b");
                area.TitleTextColor = Color.White;

                area.IntroType = Player.IntroTypes.WakeUp;

                area.Dreaming = false;
                area.ColorGrade = null;

                area.Mode = new ModeProperties[] {
                    new ModeProperties {
                        Path = asset.PathRelative.Substring(5),
                        Inventory = PlayerInventory.Default,
                        AudioState = new AudioState("event:/music/lvl1/main", "event:/env/amb/01_main")
                    }
                };

                area.Wipe = (Scene scene, bool wipeIn, Action onComplete)
                    => new AngledWipe(scene, wipeIn, onComplete);

                area.DarknessAlpha = 0.05f;
                area.BloomBase = 0f;
                area.BloomStrength = 1f;

                area.Jumpthru = "wood";

                area.CassseteNoteColor = Calc.HexToColor("33a9ee");
                area.CassetteSong = "event:/music/cassette/01_forsaken_city";

                // Custom values.
                if (meta != null) {
                    if (!string.IsNullOrEmpty(meta.Name))
                        area.Name = meta.Name;

                    if (!string.IsNullOrEmpty(meta.SID))
                        area.SetSID(meta.SID);

                    if (!string.IsNullOrEmpty(meta.Icon) && GFX.Gui.Has(meta.Icon))
                        area.Icon = meta.Icon;

                    area.Interlude = meta.Interlude;
                    if (!string.IsNullOrEmpty(meta.CompleteScreenName))
                        area.CompleteScreenName = meta.CompleteScreenName;

                    area.CassetteCheckpointIndex = meta.CassetteCheckpointIndex;

                    if (!string.IsNullOrEmpty(meta.TitleBaseColor))
                        area.TitleBaseColor = Calc.HexToColor(meta.TitleBaseColor);
                    if (!string.IsNullOrEmpty(meta.TitleAccentColor))
                        area.TitleAccentColor = Calc.HexToColor(meta.TitleAccentColor);
                    if (!string.IsNullOrEmpty(meta.TitleTextColor))
                        area.TitleTextColor = Calc.HexToColor(meta.TitleTextColor);

                    area.IntroType = meta.IntroType;

                    area.Dreaming = meta.Dreaming;
                    if (!string.IsNullOrEmpty(meta.ColorGrade))
                        area.ColorGrade = meta.ColorGrade;

                    area.Mode = MapMeta.Convert(meta.Modes) ?? area.Mode;

                    if (!string.IsNullOrEmpty(meta.Wipe)) {
                        // TODO: Use meta.Wipe!
                    }

                    area.DarknessAlpha = meta.DarknessAlpha;
                    area.BloomBase = meta.BloomBase;
                    area.BloomStrength = meta.BloomStrength;

                    if (!string.IsNullOrEmpty(meta.Jumpthru))
                        area.Jumpthru = meta.Jumpthru;

                    if (!string.IsNullOrEmpty(meta.CassetteNoteColor))
                        area.CassseteNoteColor = Calc.HexToColor(meta.CassetteNoteColor);
                    if (!string.IsNullOrEmpty(meta.CassetteSong))
                        area.CassetteSong = meta.CassetteSong;

                    area.MountainIdle = meta.Mountain?.Idle?.Convert() ?? area.MountainIdle;
                    area.MountainSelect = meta.Mountain?.Idle?.Convert() ?? area.MountainSelect;
                    area.MountainZoom = meta.Mountain?.Idle?.Convert() ?? area.MountainZoom;
                    area.MountainCursor = meta.Mountain?.Cursor?.ToVector3() ?? area.MountainCursor;
                    area.MountainState = meta.Mountain?.State ?? area.MountainState;

                }

                // Some of the game's code checks for [1] / [2] hardcoded.
                if (area.Mode.Length < 3) {
                    ModeProperties[] larger = new ModeProperties[3];
                    for (int i = 0; i < area.Mode.Length; i++)
                        larger[i] = area.Mode[i];
                    area.Mode = larger;
                }

                // Celeste levelset always appears first.
                if (area.GetLevelSet() == "Celeste")
                    Areas.Add(area);
                else
                    modAreas.Add(area);
            }


            // Sort and merge modAreas into Areas. Makes for easier levelset handling.
            Areas.Sort((a, b) => string.Compare(a.GetSID(), b.GetSID()));
            modAreas.Sort((a, b) => string.Compare(a.GetSID(), b.GetSID()));
            Areas.AddRange(modAreas);

            // Find duplicates and remove the earlier copy.
            for (int i = 0; i < Areas.Count; i++) {
                AreaData area = Areas[i];
                int otherIndex = Areas.FindIndex(other => other.GetSID() == area.GetSID());
                if (otherIndex < i) {
                    Areas[otherIndex] = area;
                    Areas.RemoveAt(i);
                    i--;
                }
            }

            // Remove AreaDatas which are now a mode of another AreaData.
            for (int i = 0; i < Areas.Count; i++) {
                AreaData area = Areas[i];
                int otherIndex = Areas.FindIndex(other => other.Mode.Any(otherMode => otherMode?.Path == area.Mode[0].Path));
                if (otherIndex != -1 && otherIndex != i) {
                    Areas.RemoveAt(i);
                    i--;
                }
            }

            // Update old MapData areas and load any new areas.
            for (int i = 10; i < Areas.Count; i++) {
                AreaData area = Areas[i];
                area.ID = i;
                if (area.Mode[0].MapData != null)
                    area.Mode[0].MapData.Area = area.ToKey();
                else
                    area.Mode[0].MapData = new MapData(area.ToKey());
                if (area.Interlude)
                    continue;
                for (int mode = 1; mode < area.Mode.Length; mode++) {
                    if (area.Mode[mode] == null)
                        continue;
                    if (area.Mode[mode].MapData != null)
                        area.Mode[mode].MapData.Area = area.ToKey((AreaMode) mode);
                    else
                        area.Mode[mode].MapData = new MapData(area.ToKey((AreaMode) mode));
                }
            }

            Everest.Events.AreaData.Load();
        }

        public static extern void orig_ReloadMountainViews();
        public static new void ReloadMountainViews() {
            orig_ReloadMountainViews();
            Everest.Events.AreaData.ReloadMountainViews();
        }

    }
    public static class AreaDataExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static AreaData Get(AreaStats stats)
            => patch_AreaData.Get(stats);
        public static AreaData Get(string sid)
            => patch_AreaData.Get(sid);

        public static AreaKey ToKey(this AreaData self, AreaMode mode = AreaMode.Normal)
            => new AreaKey(self.ID, mode).SetSID(self.GetSID());

        public static string GetLevelSet(this AreaData self)
            => ((patch_AreaData) self).LevelSet;

        public static string GetSID(this AreaData self)
            => ((patch_AreaData) self).SID;
        public static AreaData SetSID(this AreaData self, string value) {
            ((patch_AreaData) self).SID = value;
            return self;
        }

    }
}
