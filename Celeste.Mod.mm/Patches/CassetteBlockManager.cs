#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;
using System.IO;
using FMOD.Studio;
using Monocle;
using Celeste.Mod.Meta;

namespace Celeste {
    class patch_CassetteBlockManager : CassetteBlockManager {

        private float tempoMult;
        private int beatsPerTick;
        private int ticksPerSwap;
        private int blocks;
        private int beatsMax;

        private int currentIndex;
        private int beatIndex;
        private float beatTimer;
        private int leadBeats;
        private EventInstance sfx;
        private EventInstance snapshot;

        public patch_CassetteBlockManager(string levelID)
            : base(levelID) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Awake(Scene scene);
        public override void Awake(Scene scene) {
            orig_Awake(scene);

            tempoMult = 1f;
            leadBeats = 16;
            beatsPerTick = 4;
            ticksPerSwap = 2;
            blocks = 2;
            beatsMax = 256;

            MapMetaCassetteModifier meta = AreaData.Get((Scene as Level).Session.Area).GetMeta()?.CassetteModifier;
            if (meta != null) {
                tempoMult = meta.TempoMult;
                leadBeats = meta.LeadBeats;
                beatsPerTick = meta.BeatsPerTick;
                ticksPerSwap = meta.TicksPerSwap;
                blocks = meta.Blocks;
                beatsMax = meta.BeatsMax;
            }
        }

        [MonoModReplace]
        public new void AdvanceMusic(float time) {
            beatTimer += time;

            if (beatTimer < 0.166666672f)
                return;

            beatTimer -= 0.166666672f;
            beatIndex++;
            beatIndex %= beatsMax;

            if (beatIndex % (beatsPerTick * ticksPerSwap) == 0) {
                currentIndex++;
                currentIndex %= blocks;
                SetActiveIndex(currentIndex);
                Audio.Play("event:/game/general/cassette_block_switch_2");
                Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);

            } else {
                if ((beatIndex + 1) % (beatsPerTick * ticksPerSwap) == 0) {
                    SetWillActivate((currentIndex + 1) % blocks);
                }
                if (beatIndex % beatsPerTick == 0) {
                    Audio.Play("event:/game/general/cassette_block_switch_1");
                }
            }

            if (leadBeats > 0) {
                leadBeats--;
                if (leadBeats == 0) {
                    beatIndex = 0;
                    sfx.start();
                }
            }

            if (leadBeats <= 0) {
                sfx.setParameterValue("sixteenth_note", beatIndex + 1);
            }
        }

        [MonoModReplace]
        public new void OnLevelStart() {
            /*
            if (beatIndex % 8 >= 5) {
                currentIndex = 0;
            } else {
                currentIndex = 1;
            }
            */

            // This is accurate to the above original code, but could result in a few edge case inaccuracies with custom values.
            currentIndex = blocks - 1 - ((beatIndex / beatsPerTick) % blocks);

            SilentUpdateBlocks();
        }

        [MonoModIgnore]
        private extern void SilentUpdateBlocks();

    }
}
