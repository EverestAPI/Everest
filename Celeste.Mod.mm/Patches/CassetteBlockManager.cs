#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

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
        private int maxBeat;
        private int beatIndexMax;
        private bool isLevelMusic;

        private int currentIndex;
        private int beatIndex;
        private float beatTimer;
        private int leadBeats;
        private EventInstance sfx;
        private EventInstance snapshot;

        public patch_CassetteBlockManager()
            : base() {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Awake(Scene scene);
        public override void Awake(Scene scene) {
            orig_Awake(scene);

            beatsPerTick = 4;
            ticksPerSwap = 2;
            beatIndexMax = 256;

            MapMetaCassetteModifier meta = AreaData.Get((Scene as Level).Session).GetMeta()?.CassetteModifier;
            if (meta != null) {
                tempoMult = meta.TempoMult;
                leadBeats = meta.LeadBeats;
                beatsPerTick = meta.BeatsPerTick;
                ticksPerSwap = meta.TicksPerSwap;
                maxBeat = meta.Blocks;
                beatIndexMax = meta.BeatsMax;
            }
        }

        [MonoModReplace]
        public new void AdvanceMusic(float time) {
            beatTimer += time;

            if (beatTimer < 0.166666672f)
                return;

            beatTimer -= 0.166666672f;
            beatIndex++;
            beatIndex %= beatIndexMax;

            if (beatIndex % (beatsPerTick * ticksPerSwap) == 0) {
                currentIndex++;
                currentIndex %= maxBeat;
                SetActiveIndex(currentIndex);
                if(!isLevelMusic)
                    Audio.Play("event:/game/general/cassette_block_switch_2");
                Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);

            } else {
                if ((beatIndex + 1) % (beatsPerTick * ticksPerSwap) == 0) {
                    SetWillActivate((currentIndex + 1) % maxBeat);
                }
                if (beatIndex % beatsPerTick == 0 && !isLevelMusic) {
                    Audio.Play("event:/game/general/cassette_block_switch_1");
                }
            }

            if (leadBeats > 0) {
                leadBeats--;
                if (leadBeats == 0) {
                    beatIndex = 0;
                    if(!isLevelMusic)
                        sfx.start();
                }
            }

            if (leadBeats <= 0) {
                sfx.setParameterValue("sixteenth_note", GetSixteenthNote());
            }
        }

        [MonoModReplace]
        public new void OnLevelStart() {
            maxBeat = SceneAs<Level>().CassetteBlockBeats;
            tempoMult = SceneAs<Level>().CassetteBlockTempo;

            if (SceneAs<Level>().Session.Area.GetLevelSet() == "Celeste") {
                if (beatIndex % 8 >= 5) {
                    currentIndex = maxBeat - 2;
                } else {
                    currentIndex = maxBeat - 1;
                }
            } else {
                currentIndex = maxBeat - 1 - ((beatIndex / beatsPerTick) % maxBeat);
            }

            SilentUpdateBlocks();
        }

        [MonoModIgnore]
        private extern void SilentUpdateBlocks();

    }
}
