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
using System.Reflection;
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
                    return "Celeste";
                int lastIndexOfSlash = sid.LastIndexOf('/');
                if (lastIndexOfSlash == -1)
                    return "";
                return sid.Substring(0, lastIndexOfSlash);
            }
        }

        public MapMeta Meta;

        // Required for the journal to hide areas outside of the current levelset.

        [MonoModLinkFrom("System.Boolean Celeste.AreaData::Interlude_Unsafe")]
        public new bool Interlude;

        [MonoModRemove]
        public bool Interlude_Unsafe;

        [MonoModLinkFrom("System.Boolean Celeste.AreaData::Interlude")]
        public bool Interlude_Safe {
            get {
                return 
                    Interlude_Unsafe ||
                    (SaveData.Instance != null && SaveData.Instance.GetLevelSet() != LevelSet);
            }
            set {
                Interlude_Unsafe = value;
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
            if (area.GetSID() == null)
                return Get(area.ID);
            return Get(area.GetSID());
        }

        [MonoModReplace]
        public static new AreaData Get(int id) {
            return Areas[id];
        }

        public static AreaData Get(AreaStats stats) {
            if (stats.GetSID() == null)
                return Get(stats.ID);
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

            foreach (ModAsset asset in Everest.Content.Map.Values.Where(asset => asset.Type == typeof(AssetTypeMap))) {
                string path = asset.PathVirtual.Substring(5);

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
                        Inventory = PlayerInventory.Default,
                        AudioState = new AudioState(Sfxs.music_city, Sfxs.env_amb_00_main)
                    }
                };

                area.Wipe = (Scene scene, bool wipeIn, Action onComplete)
                    => new AngledWipe(scene, wipeIn, onComplete);

                area.DarknessAlpha = 0.05f;
                area.BloomBase = 0f;
                area.BloomStrength = 1f;

                area.Jumpthru = "wood";

                area.CassseteNoteColor = Calc.HexToColor("33a9ee");
                area.CassetteSong = Sfxs.cas_01_forsaken_city;

                // Custom values can be set via the MapMeta.
                MapMeta meta = asset.GetMeta<MapMeta>() ?? new MapMeta();
                meta?.ApplyTo(area);

                if (string.IsNullOrEmpty(area.Mode[0].Path))
                    area.Mode[0].Path = asset.PathVirtual.Substring(5);

		// Some of the game's code checks for [1] / [2] explicitly.
		// Let's just provide null modes to fill any gaps.
                meta.Modes = meta.Modes ?? new MapMetaModeProperties[3];
                if (meta.Modes.Length < 3) {
                    MapMetaModeProperties[] larger = new MapMetaModeProperties[3];
                    for (int i = 0; i < meta.Modes.Length; i++)
                        larger[i] = meta.Modes[i];
                    meta.Modes = larger;
                }
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


            // Merge modAreas into Areas.
            Areas.AddRange(modAreas);

            // Find duplicates and remove any earlier copies.
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
            // This can happen late as the map data (.bin) can contain additional metadata.
            for (int i = 0; i < Areas.Count; i++) {
                AreaData area = Areas[i];
                string path = area.Mode[0].Path;
                int otherIndex = Areas.FindIndex(other => other.Mode.Any(otherMode => otherMode?.Path == path));
                if (otherIndex != -1 && otherIndex != i) {
                    Areas.RemoveAt(i);
                    i--;
                    continue;
                }

                // Also check for .bins possibly belonging to A side .bins by their path and lack of existing modes.
                for (int ii = 0; ii < Areas.Count; ii++) {
                    AreaData other = Areas[ii];
                    if (!other.HasMode(AreaMode.BSide) && path == (other.Mode[0].Path + "-B")) {
                        if (other.Mode[1] == null)
                            other.Mode[1] = new ModeProperties {
                                Inventory = PlayerInventory.Default,
                                AudioState = new AudioState(Sfxs.music_city, Sfxs.env_amb_00_main)
                            };
                        other.Mode[1].Path = path;
                        Areas.RemoveAt(i);
                        i--;
                        break;
                    }
                    if (!other.HasMode(AreaMode.CSide) && path == (other.Mode[0].Path + "-C")) {
                        if (other.Mode[2] == null)
                            other.Mode[2] = new ModeProperties {
                                Inventory = PlayerInventory.Default,
                                AudioState = new AudioState(Sfxs.music_city, Sfxs.env_amb_00_main)
                            };
                        other.Mode[2].Path = path;
                        Areas.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }

            // Sort areas.
            Areas.Sort(AreaComparison);

            // Update old MapData areas and load any new areas.
            for (int i = 0; i < Areas.Count; i++) {
                AreaData area = Areas[i];
                area.ID = i;

                // Add the A side MapData or update its area key.
                if (area.Mode[0].MapData != null)
                    area.Mode[0].MapData.Area = area.ToKey();
                else
                    area.Mode[0].MapData = new MapData(area.ToKey());

                if (area.Interlude)
                    continue;
                
                // A and (some) B sides have PoemIDs. Can be overridden via empty PoemID.
                if (area.Mode[0].PoemID == null)
                    area.Mode[0].PoemID = area.GetSID().DialogKeyify() + "_A";
                if (area.Mode[1] != null &&
                    area.Mode[1].PoemID == null) {
                    area.Mode[1].PoemID = area.GetSID().DialogKeyify() + "_B";
                }

                // Update all other existing mode's area keys.
                for (int mode = 1; mode < area.Mode.Length; mode++) {
                    if (area.Mode[mode] == null)
                        continue;
                    if (area.Mode[mode].MapData != null)
                        area.Mode[mode].MapData.Area = area.ToKey((AreaMode) mode);
                    else
                        area.Mode[mode].MapData = new MapData(area.ToKey((AreaMode) mode));
                }
            }
        }

        private static int AreaComparison(AreaData a, AreaData b) {
            string aSet = a.GetLevelSet();
            string bSet = b.GetLevelSet();

            // Celeste appears before everything else.
            if (aSet == "Celeste" && bSet != "Celeste")
                return -1;
            if (aSet != "Celeste" && bSet == "Celeste")
                return 1;

            // Uncategorized appears after everything else.
            if (string.IsNullOrEmpty(aSet) && !string.IsNullOrEmpty(bSet))
                return 1;
            if (!string.IsNullOrEmpty(aSet) && string.IsNullOrEmpty(bSet))
                return -1;

            return string.Compare(a.GetSID(), b.GetSID());
        }

    }
    public static class AreaDataExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static AreaData Get(AreaStats stats)
            => patch_AreaData.Get(stats);
        public static AreaData Get(string sid)
            => patch_AreaData.Get(sid);

        /// <summary>
        /// Get an AreaKey for this area.
        /// </summary>
        public static AreaKey ToKey(this AreaData self, AreaMode mode = AreaMode.Normal)
            => new AreaKey(self.ID, mode).SetSID(self.GetSID());

        /// <summary>
        /// Get the name of the level set this area belongs to.
        /// </summary>
        public static string GetLevelSet(this AreaData self)
            => ((patch_AreaData) self).LevelSet;

        /// <summary>
        /// Get the SID (string ID) of the area.
        /// </summary>
        public static string GetSID(this AreaData self)
            => ((patch_AreaData) self).SID;
        /// <summary>
        /// Set the SID (string ID) of the area.
        /// </summary>
        public static AreaData SetSID(this AreaData self, string value) {
            ((patch_AreaData) self).SID = value;
            return self;
        }

        /// <summary>
        /// Get the custom metadata if it has been loaded from the .meta or set otherwise.
        /// </summary>
        public static MapMeta GetMeta(this AreaData self)
            => ((patch_AreaData) self).Meta;
        /// <summary>
        /// Set the custom metadata.
        /// </summary>
        public static AreaData SetMeta(this AreaData self, MapMeta value) {
            ((patch_AreaData) self).Meta = value;
            return self;
        }

    }
}
