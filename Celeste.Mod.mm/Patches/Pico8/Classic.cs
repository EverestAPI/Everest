#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using MonoMod;
using System.Collections.Generic;

namespace Celeste.Pico8 {
    class patch_Classic : Classic {
        private HashSet<int> got_fruit;

        class patch_flag : flag {
            private float score;

            [MonoModLinkTo("Celeste.Pico8.Classic/ClassicObject", "System.Void init(Celeste.Pico8.Classic,Celeste.Pico8.Emulator)")]
            [MonoModRemove]
            private extern void base_init(Classic g, Emulator e);

            [MonoModReplace]
            public override void init(Classic g, Emulator e) {
                base_init(g, e);
                x += 5f;
                score = ((patch_Classic) G).got_fruit.Count;
                Stats.Increment(Stat.PICO_COMPLETES);
                // only unlock pico8 achievement if there's no custom tilemap
                if (!Everest.Content.TryGet<AssetTypeText>("Pico8Tilemap", out _)) {
                    Achievements.Register(Achievement.PICO8);
                }
            }
        }
    }
}
