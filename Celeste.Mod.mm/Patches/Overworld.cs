#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste;
using Celeste.Mod;
using Celeste.Mod.Meta;
using Celeste.Mod.UI;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste {
    class patch_Overworld : Overworld {
        private bool customizedChapterSelectMusic = false;

#pragma warning disable CS0649 // variable defined in vanilla
        private Snow3D Snow3D;
#pragma warning restore CS0649

        public Dictionary<Type, OuiPropertiesAttribute> UIProperties { get; set; }

        public patch_Overworld(OverworldLoader loader)
            : base(loader) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Adding this method is required so that BeforeRenderHooks work properly.
        public override void BeforeRender() {
            foreach (Component component in Tracker.GetComponents<BeforeRenderHook>()) {
                BeforeRenderHook beforeRenderHook = (BeforeRenderHook) component;
                if (beforeRenderHook.Visible) {
                    beforeRenderHook.Callback();
                }
            }
            base.BeforeRender();
        }

        public extern void orig_Update();
        public override void Update() {
            lock (AssetReloadHelper.AreaReloadLock) {
                orig_Update();

                // if the mountain model is currently fading, use the one currently displayed, not the one currently selected, which is different if the fade isn't done yet.
                patch_AreaData currentAreaData = null;
                string currentlyDisplayedSID = (Mountain?.Model as patch_MountainModel)?.PreviousSID;
                if (currentlyDisplayedSID != null) {
                    // use the settings of the currently displayed mountain
                    currentAreaData = patch_AreaData.Get(currentlyDisplayedSID);
                } else if (SaveData.Instance != null) {
                    // use the settings of the currently selected map
                    currentAreaData = patch_AreaData.Get(SaveData.Instance.LastArea);
                }
                MapMetaMountain mountainMetadata = currentAreaData?.Meta?.Mountain;

                Snow3D.Visible = mountainMetadata?.ShowSnow ?? true;

                if (string.IsNullOrEmpty(Audio.CurrentMusic)) {
                    // don't change music if no music is currently playing
                    return;
                }

                if (SaveData.Instance != null && IsCurrent(o => UIProperties?.GetValueOrDefault(o.GetType())?.PlayCustomMusic ?? false)) {
                    string backgroundMusic = mountainMetadata?.BackgroundMusic;
                    string backgroundAmbience = mountainMetadata?.BackgroundAmbience;
                    if (backgroundMusic != null || backgroundAmbience != null) {
                        // current map has custom background music
                        Audio.SetMusic(backgroundMusic ?? "event:/music/menu/level_select");
                        Audio.SetAmbience(backgroundAmbience ?? "event:/env/amb/worldmap");
                        customizedChapterSelectMusic = true;
                    } else {
                        // current map has no custom background music
                        restoreNormalMusicIfCustomized();
                    }

                    foreach (KeyValuePair<string, float> musicParam in mountainMetadata?.BackgroundMusicParams ?? new Dictionary<string, float>()) {
                        Audio.SetMusicParam(musicParam.Key, musicParam.Value);
                    }
                } else {
                    // no save is loaded or we are not in chapter select
                    restoreNormalMusicIfCustomized();
                }
            }
        }

        public bool IsCurrent(Func<Oui, bool> predicate) {
            if (Current != null) {
                return predicate(Current);
            }
            return predicate(Last);
        }

        public Oui RegisterOui(Type type) {
            Oui oui = (Oui) Activator.CreateInstance(type);
            oui.Visible = false;
            Add(oui);
            UIs.Add(oui);
            UIProperties ??= new() {
                { typeof(OuiChapterSelect), new(true) },
                { typeof(OuiChapterPanel), new(true) },
                { typeof(OuiMapList), new(true) },
                { typeof(OuiMapSearch), new(true) },
                { typeof(OuiJournal), new(true) }
            };
            foreach (OuiPropertiesAttribute attrib in type.GetCustomAttributes<OuiPropertiesAttribute>()) {
                UIProperties[type] = attrib;
            }
            return oui;
        }

        [MonoModIgnore]
        [PatchOverworldRegisterOui]
        public new extern void ReloadMenus(StartMode startMode = StartMode.Titlescreen);

        public extern void orig_ReloadMountainStuff();
        public new void ReloadMountainStuff() {
            orig_ReloadMountainStuff();

            // reload all loaded custom mountain models as well.
            foreach (ObjModel customMountainModel in MTNExt.ObjModelCache.Values) {
                customMountainModel.ReassignVertices();
            }
        }

        public extern void orig_End();
        public override void End() {
            orig_End();

            if (!EnteringPico8) {
                Remove(Snow);
                ((patch_RendererList) (object) RendererList).UpdateLists();
                Snow = null;
            }
        }

        private void restoreNormalMusicIfCustomized() {
            if (customizedChapterSelectMusic) {
                SetNormalMusic();
                customizedChapterSelectMusic = false;
            }
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Adjust the Overworld.ReloadMenus method to use the RegisterOui function, rather than registering manually
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchOverworldRegisterOuiFunction))]
    class PatchOverworldRegisterOuiAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchOverworldRegisterOuiFunction(ILContext il, CustomAttribute attrib) {
            MethodDefinition registerOuiMethod = MonoModRule.Modder.Module.GetType("Celeste.Overworld").FindMethod(nameof(patch_Overworld.RegisterOui));
            //MethodInfo registerOuiMethod = typeof(patch_Overworld).GetMethod(nameof(patch_Overworld.RegisterOui));
            
            ILCursor c = new(il);
            c.GotoNext(MoveType.Before,
                instr => instr.MatchLdloc(4),
                instr => instr.MatchCall("System.Activator", nameof(Activator.CreateInstance)));
            // We have Oui oui = (Oui)Activator.CreateInstance(type);
            // Replace the right side of the equals with our register function

            // this.
            c.EmitLdarg0();
            // Skip past `type` argument
            c.GotoNext().GotoNext();
            c.Remove();
            c.Remove();
            // RegisterOui()
            c.EmitCall(registerOuiMethod);
            // Skip past the "Oui oui ="
            c.GotoNext().GotoNext();
            // The next 10 instructions are already present in RegisterOui and can be removed
            c.RemoveRange(10);
        }
    }
}
