#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    abstract class patch_PlayerPlayback : PlayerPlayback {

        public patch_PlayerPlayback(EntityData e, Vector2 offset)
            : base(e, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData e, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData e, Vector2 offset) {
            string tutorialName = e.Attr("tutorial");
            if (PlaybackData.Tutorials.ContainsKey(tutorialName)) {
                orig_ctor(e, offset);
            } else {
                patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_missingtutorial").Replace("((tutorial))", tutorialName);
                Logger.Warn("PlayerPlayback", $"Failed to load tutorial '{tutorialName}'");
                throw new KeyNotFoundException();
            }
        }
    }
}
