#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_Overworld : Overworld {

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

                if (string.IsNullOrEmpty(Audio.CurrentMusic)) {
                    // don't change music if no music is currently playing
                    return;
                }

                if (SaveData.Instance != null && (IsCurrent<OuiChapterSelect>() || IsCurrent<OuiChapterPanel>() || IsCurrent<OuiMapList>() || IsCurrent<OuiMapSearch>())) {
                    string backgroundMusic = AreaData.Get(SaveData.Instance.LastArea)?.GetMeta()?.Mountain?.BackgroundMusic;
                    if (backgroundMusic != null) {
                        // current map has custom background music
                        Audio.SetMusic(backgroundMusic);
                    } else {
                        // current map has no custom background music
                        SetNormalMusic();
                    }
                } else {
                    // no save is loaded or we are not in chapter select
                    SetNormalMusic();
                }
            }
        }
    }
}
