#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod.Entities;
using Celeste.Mod.Meta;
using FMOD.Studio;
using Monocle;
using MonoMod;

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
        private int beatIndexOffset;
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
            patch_AreaData area = patch_AreaData.Get(scene);
            if (area.CassetteSong == "-" || string.IsNullOrWhiteSpace(area.CassetteSong))
                area.CassetteSong = null;

            orig_Awake(scene);

            beatsPerTick = 4;
            ticksPerSwap = 2;
            beatIndexMax = 256;

            MapMetaCassetteModifier meta = area.Meta?.CassetteModifier;
            if (meta != null) {
                if (meta.OldBehavior) {
                    tempoMult = meta.TempoMult;
                    maxBeat = meta.Blocks;
                }
                leadBeats = meta.LeadBeats;
                beatsPerTick = meta.BeatsPerTick;
                ticksPerSwap = meta.TicksPerSwap;
                beatIndexMax = meta.BeatsMax;
                beatIndexOffset = meta.BeatIndexOffset;

                if (meta.ActiveDuringTransitions) {
                    TransitionListener listener = Get<TransitionListener>();

                    if (listener != null) {
                        listener.OnOut = _ => {
                            if (Scene != null) {
                                Update();
                            }
                        };
                    }
                }
            }
        }

        [MonoModLinkTo("Monocle.Entity", "Update")]
        [MonoModIgnore]
        public extern void base_Update();
        [MonoModReplace]
        public override void Update() {
            base_Update();
            if (isLevelMusic) {
                sfx = Audio.CurrentMusicEventInstance;
            }

            if (sfx == null && !isLevelMusic) {
                string cassetteSong = AreaData.Areas[SceneAs<Level>().Session.Area.ID].CassetteSong;
                sfx = Audio.CreateInstance(cassetteSong);
                Audio.Play(SFX.game_gen_cassetteblock_switch_2);

                if (leadBeats == 0) {
                    beatIndex = 0;
                    sfx?.start();
                }
            } else {
                AdvanceMusic(Engine.DeltaTime * tempoMult);
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
                if (!isLevelMusic)
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
                    if (!isLevelMusic)
                        sfx?.start();
                }
            }

            if (leadBeats <= 0) {
                sfx?.setParameterValue("sixteenth_note", GetSixteenthNote());
            }
        }

        [MonoModReplace]
        public new int GetSixteenthNote() {
            return (beatIndex + beatIndexOffset) % beatIndexMax + 1;
        }

        [MonoModReplace]
        public new void OnLevelStart() {
            Level level = Scene as Level;
            MapMetaCassetteModifier meta = patch_AreaData.Get(level.Session).Meta?.CassetteModifier;

            if (meta != null && meta.OldBehavior) {
                currentIndex = maxBeat - 1 - ((beatIndex / beatsPerTick) % maxBeat);

            } else {
                maxBeat = level.CassetteBlockBeats;
                tempoMult = level.CassetteBlockTempo;

                if (beatIndex % (beatsPerTick * ticksPerSwap) > ((beatsPerTick * ticksPerSwap) / 2)) {
                    currentIndex = maxBeat - 2;
                } else {
                    currentIndex = maxBeat - 1;
                }
            }

            SilentUpdateBlocks();
        }

        [MonoModReplace]
        private void SilentUpdateBlocks() {
            foreach (CassetteBlock entity in Scene.Tracker.GetEntities<CassetteBlock>()) {
                if (entity.ID.Level == SceneAs<Level>().Session.Level) {
                    entity.SetActivatedSilently(entity.Index == currentIndex);
                }
            }

            foreach (CassetteListener listener in Scene.Tracker.GetComponents<CassetteListener>()) {
                if (listener.ID.ID == EntityID.None.ID || listener.ID.Level == SceneAs<Level>().Session.Level) {
                    listener.Start(listener.Index == currentIndex);
                }
            }
        }

        [MonoModReplace]
        public new void SetActiveIndex(int index) {
            foreach (CassetteBlock entity in Scene.Tracker.GetEntities<CassetteBlock>()) {
                entity.Activated = entity.Index == index;
            }

            foreach (CassetteListener listener in Scene.Tracker.GetComponents<CassetteListener>()) {
                listener.SetActivated(listener.Index == index);
            }
        }

        [MonoModReplace]
        public new void SetWillActivate(int index) {
            foreach (CassetteBlock entity in Scene.Tracker.GetEntities<CassetteBlock>()) {
                if (entity.Index == index || entity.Activated) {
                    entity.WillToggle();
                }
            }

            foreach (CassetteListener listener in Scene.Tracker.GetComponents<CassetteListener>()) {
                if (listener.Index == index || listener.Activated) {
                    listener.WillToggle();
                }
            }
        }

        [MonoModReplace]
        public new void StopBlocks() {
            foreach (CassetteBlock entity in base.Scene.Tracker.GetEntities<CassetteBlock>()) {
                entity.Finish();
            }
            foreach (CassetteListener listener in base.Scene.Tracker.GetComponents<CassetteListener>()) {
                listener.Finish();
            }
            if (!isLevelMusic) {
                Audio.Stop(sfx);
            }
        }
    }
}
