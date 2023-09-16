using Celeste.Mod.Entities;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Helpers {
    public static class TypeHelper {
        private static Dictionary<string, Type> entityDataNameToType;

        /// <summary>
        /// Gets a raw type from a CustomEntityAttribute name (i.e. "spinner" => typeof(CrystalStaticSpinner)). 
        /// Does not guarantee a CustomEntityAttribute will be referenced on load, due to CustomEntity Manual methods.
        /// If you know an Entity came from a specific name, use GetCSTypeFromEntityDataName
        /// </summary>
        /// <param name="name">the EntityData name</param>
        /// <param name="type">the output C# type</param>
        /// <returns></returns>
        public static bool GetRawTypeFromCustomEntityAttribute(string name, out Type type) {
            type = null;
            if (entityDataNameToType.ContainsKey(name)) {
                type = entityDataNameToType[name];
                return true;
            }
            return false;
        }

        internal static void BakeVanillaEntityData() {
            entityDataNameToType = new Dictionary<string, Type>() {
                ["checkpoint"] = typeof(Checkpoint),
                ["jumpThru"] = typeof(JumpthruPlatform),
                ["refill"] = typeof(Refill),
                ["infiniteStar"] = typeof(FlyFeather),
                ["strawberry"] = typeof(Strawberry),
                ["memorialTextController"] = typeof(Strawberry),
                ["goldenBerry"] = typeof(Strawberry),
                ["summitgem"] = typeof(SummitGem),
                ["blackGem"] = typeof(HeartGem),
                ["dreamHeartGem"] = typeof(DreamHeartGem),
                ["spring"] = typeof(Spring),
                ["wallSpringLeft"] = typeof(Spring),
                ["wallSpringRight"] = typeof(Spring),
                ["fallingBlock"] = typeof(FallingBlock),
                ["zipMover"] = typeof(ZipMover),
                ["crumbleBlock"] = typeof(CrumblePlatform),
                ["dreamBlock"] = typeof(DreamBlock),
                ["touchSwitch"] = typeof(TouchSwitch),
                ["switchGate"] = typeof(SwitchGate),
                ["negaBlock"] = typeof(NegaBlock),
                ["key"] = typeof(Key),
                ["lockBlock"] = typeof(LockBlock),
                ["movingPlatform"] = typeof(MovingPlatform),
                ["rotatingPlatforms"] = typeof(RotatingPlatform),
                ["blockField"] = typeof(BlockField),
                ["cloud"] = typeof(Cloud),
                ["booster"] = typeof(Booster),
                ["moveBlock"] = typeof(MoveBlock),
                ["light"] = typeof(PropLight),
                ["switchBlock"] = typeof(SwapBlock),
                ["swapBlock"] = typeof(SwapBlock),
                ["dashSwitchH"] = typeof(DashSwitch),
                ["dashSwitchV"] = typeof(DashSwitch),
                ["templeGate"] = typeof(TempleGate),
                ["torch"] = typeof(Torch),
                ["templeCrackedBlock"] = typeof(TempleCrackedBlock),
                ["seekerBarrier"] = typeof(SeekerBarrier),
                ["theoCrystal"] = typeof(TheoCrystal),
                ["glider"] = typeof(Glider),
                ["theoCrystalPedestal"] = typeof(TheoCrystalPedestal),
                ["badelineBoost"] = typeof(BadelineBoost),
                ["cassette"] = typeof(Cassette),
                ["cassetteBlock"] = typeof(CassetteBlock),
                ["wallBooster"] = typeof(WallBooster),
                ["bounceBlock"] = typeof(BounceBlock),
                ["coreModeToggle"] = typeof(CoreModeToggle),
                ["iceBlock"] = typeof(IceBlock),
                ["fireBarrier"] = typeof(FireBarrier),
                ["eyebomb"] = typeof(Puffer),
                ["flingBird"] = typeof(FlingBird),
                ["flingBirdIntro"] = typeof(FlingBirdIntro),
                ["birdPath"] = typeof(BirdPath),
                ["lightningBlock"] = typeof(LightningBreakerBox),
                ["spikesUp"] = typeof(Spikes),
                ["spikesDown"] = typeof(Spikes),
                ["spikesLeft"] = typeof(Spikes),
                ["spikesRight"] = typeof(Spikes),
                ["triggerSpikesUp"] = typeof(TriggerSpikes),
                ["triggerSpikesDown"] = typeof(TriggerSpikes),
                ["triggerSpikesRight"] = typeof(TriggerSpikes),
                ["triggerSpikesLeft"] = typeof(TriggerSpikes),
                ["darkChaser"] = typeof(BadelineOldsite),
                ["rotateSpinner"] = typeof(BladeRotateSpinner),
                ["trackSpinner"] = typeof(TrackSpinner),
                ["spinner"] = typeof(CrystalStaticSpinner),
                ["sinkingPlatform"] = typeof(SinkingPlatform),
                ["friendlyGhost"] = typeof(AngryOshiro),
                ["seeker"] = typeof(Seeker),
                ["seekerStatue"] = typeof(SeekerStatue),
                ["slider"] = typeof(Slider),
                ["templeBigEyeball"] = typeof(TempleBigEyeball),
                ["crushBlock"] = typeof(CrushBlock),
                ["bigSpinner"] = typeof(Bumper),
                ["starJumpBlock"] = typeof(StarJumpBlock),
                ["floatySpaceBlock"] = typeof(FloatySpaceBlock),
                ["glassBlock"] = typeof(GlassBlock),
                ["goldenBlock"] = typeof(GoldenBlock),
                ["fireBall"] = typeof(FireBall),
                ["risingLava"] = typeof(RisingLava),
                ["sandwichLava"] = typeof(SandwichLava),
                ["killbox"] = typeof(Killbox),
                ["fakeHeart"] = typeof(FakeHeart),
                ["lightning"] = typeof(Lightning),
                ["finalBoss"] = typeof(FinalBoss),
                ["finalBossFallingBlock"] = typeof(FallingBlock),
                ["finalBossMovingBlock"] = typeof(FinalBossMovingBlock),
                ["fakeWall"] = typeof(FakeWall),
                ["fakeBlock"] = typeof(FakeWall),
                ["dashBlock"] = typeof(DashBlock),
                ["invisibleBarrier"] = typeof(InvisibleBarrier),
                ["exitBlock"] = typeof(ExitBlock),
                ["conditionBlock"] = typeof(ExitBlock),
                ["coverupWall"] = typeof(CoverupWall),
                ["crumbleWallOnRumble"] = typeof(CrumbleWallOnRumble),
                ["ridgeGate"] = typeof(RidgeGate),
                ["tentacles"] = typeof(Tentacles),
                ["starClimbController"] = typeof(StarClimbGraphicsController),
                ["playerSeeker"] = typeof(PlayerSeeker),
                ["chaserBarrier"] = typeof(ChaserBarrier),
                ["introCrusher"] = typeof(IntroCrusher),
                ["bridge"] = typeof(Bridge),
                ["bridgeFixed"] = typeof(BridgeFixed),
                ["bird"] = typeof(BirdNPC),
                ["introCar"] = typeof(IntroCar),
                ["memorial"] = typeof(Memorial),
                ["wire"] = typeof(Wire),
                ["cobweb"] = typeof(Cobweb),
                ["lamp"] = typeof(Lamp),
                ["hanginglamp"] = typeof(HangingLamp),
                ["hahaha"] = typeof(Hahaha),
                ["bonfire"] = typeof(Bonfire),
                ["payphone"] = typeof(Payphone),
                ["colorSwitch"] = typeof(ClutterSwitch),
                ["clutterDoor"] = typeof(ClutterDoor),
                ["dreammirror"] = typeof(DreamMirror),
                ["resortmirror"] = typeof(ResortMirror),
                ["towerviewer"] = typeof(Lookout),
                ["picoconsole"] = typeof(PicoConsole),
                ["wavedashmachine"] = typeof(WaveDashTutorialMachine),
                ["yellowBlocks"] = typeof(ClutterBlockBase),
                ["redBlocks"] = typeof(ClutterBlockBase),
                ["greenBlocks"] = typeof(ClutterBlockBase),
                ["oshirodoor"] = typeof(MrOshiroDoor),
                ["templeMirrorPortal"] = typeof(TempleMirrorPortal),
                ["reflectionHeartStatue"] = typeof(ReflectionHeartStatue),
                ["resortRoofEnding"] = typeof(ResortRoofEnding),
                ["gondola"] = typeof(Gondola),
                ["birdForsakenCityGem"] = typeof(ForsakenCitySatellite),
                ["whiteblock"] = typeof(WhiteBlock),
                ["plateau"] = typeof(Plateau),
                ["soundSource"] = typeof(SoundSourceEntity),
                ["templeMirror"] = typeof(TempleMirror),
                ["templeEye"] = typeof(TempleEye),
                ["clutterCabinet"] = typeof(ClutterCabinet),
                ["floatingDebris"] = typeof(FloatingDebris),
                ["foregroundDebris"] = typeof(ForegroundDebris),
                ["moonCreature"] = typeof(MoonCreature),
                ["lightbeam"] = typeof(LightBeam),
                ["door"] = typeof(Door),
                ["trapdoor"] = typeof(Trapdoor),
                ["resortLantern"] = typeof(ResortLantern),
                ["water"] = typeof(Water),
                ["waterfall"] = typeof(WaterFall),
                ["bigWaterfall"] = typeof(BigWaterfall),
                ["clothesline"] = typeof(Clothesline),
                ["cliffflag"] = typeof(CliffFlags),
                ["cliffside_flag"] = typeof(CliffsideWindFlag),
                ["flutterbird"] = typeof(FlutterBird),
                ["SoundTest3d"] = typeof(_3dSoundTest),
                ["SummitBackgroundManager"] = typeof(AscendManager),
                ["summitGemManager"] = typeof(SummitGem),
                ["heartGemDoor"] = typeof(HeartGemDoor),
                ["summitcheckpoint"] = typeof(SummitCheckpoint),
                ["summitcloud"] = typeof(SummitCloud),
                ["coreMessage"] = typeof(CoreMessage),
                ["playbackTutorial"] = typeof(PlayerPlayback),
                ["playbackBillboard"] = typeof(PlaybackBillboard),
                ["cutsceneNode"] = typeof(CutsceneNode),
                ["kevins_pc"] = typeof(KevinsPC),
                ["powerSourceNumber"] = typeof(PowerSourceNumber),
                ["npc"] = typeof(NPC),
                ["eventTrigger"] = typeof(EventTrigger),
                ["musicFadeTrigger"] = typeof(MusicFadeTrigger),
                ["musicTrigger"] = typeof(MusicTrigger),
                ["altMusicTrigger"] = typeof(AltMusicTrigger),
                ["cameraOffsetTrigger"] = typeof(CameraOffsetTrigger),
                ["lightFadeTrigger"] = typeof(LightFadeTrigger),
                ["bloomFadeTrigger"] = typeof(BloomFadeTrigger),
                ["cameraTargetTrigger"] = typeof(CameraTargetTrigger),
                ["cameraAdvanceTargetTrigger"] = typeof(CameraAdvanceTargetTrigger),
                ["respawnTargetTrigger"] = typeof(RespawnTargetTrigger),
                ["changeRespawnTrigger"] = typeof(ChangeRespawnTrigger),
                ["windTrigger"] = typeof(WindTrigger),
                ["windAttackTrigger"] = typeof(WindAttackTrigger),
                ["minitextboxTrigger"] = typeof(MiniTextboxTrigger),
                ["oshiroTrigger"] = typeof(OshiroTrigger),
                ["interactTrigger"] = typeof(InteractTrigger),
                ["checkpointBlockerTrigger"] = typeof(CheckpointBlockerTrigger),
                ["lookoutBlocker"] = typeof(LookoutBlocker),
                ["stopBoostTrigger"] = typeof(StopBoostTrigger),
                ["noRefillTrigger"] = typeof(NoRefillTrigger),
                ["ambienceParamTrigger"] = typeof(AmbienceParamTrigger),
                ["creditsTrigger"] = typeof(CreditsTrigger),
                ["goldenBerryCollectTrigger"] = typeof(GoldBerryCollectTrigger),
                ["moonGlitchBackgroundTrigger"] = typeof(MoonGlitchBackgroundTrigger),
                ["blackholeStrength"] = typeof(BlackholeStrengthTrigger),
                ["rumbleTrigger"] = typeof(RumbleTrigger),
                ["birdPathTrigger"] = typeof(BirdPathTrigger),
                ["spawnFacingTrigger"] = typeof(SpawnFacingTrigger),
                ["detachFollowersTrigger"] = typeof(DetachStrawberryTrigger)
            };
        }

        public static void AddToDataNameType(Dictionary<string, Type> entityDataNameToType, bool overwrite = false) {
            foreach(var kvp in entityDataNameToType) {
                if(overwrite || !entityDataNameToType.ContainsKey(kvp.Key))
                    entityDataNameToType[kvp.Key] = kvp.Value;
            }
        }

        public static bool GetTypeFromDataName(string entityDataName, out Type type) {
            return entityDataNameToType.TryGetValue(entityDataName, out type);
        }

        public static bool CheckEntityFromDataName(Entity entity, string entityDataName, out Type type) {
            type = null;
            if ((entity as patch_Entity).__EntityData is { } ed && ed.Name == entityDataName) {
                type = entity.GetType();
                return true;
            }
            return false;
        }

        public static bool CheckEntityFromData(Entity entity, Predicate<EntityData> predicate, out Type type) {
            type = null;
            if ((entity as patch_Entity).__EntityData is { } ed && predicate(ed)) {
                type = entity.GetType();
                return true;
            }
            return false;
        }
    }
}
