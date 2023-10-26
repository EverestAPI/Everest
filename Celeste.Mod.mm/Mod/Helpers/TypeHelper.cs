using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Helpers {
    public static class TypeHelper {

        public static Dictionary<string, Type> typeFullNameToTypeCache;

        public static Dictionary<string, Type> entityDataNameToType;
        /// <summary>
        /// Bakes the vanilla EntityData names to their corresponding types
        /// </summary>
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

        public static void Add_DataNameTypeMap(string name, Type type, bool overwrite = false) {
            if (overwrite || !entityDataNameToType.ContainsKey(name))
                entityDataNameToType[name] = type;
        }

        public static void Add_DataNameTypeMap(Dictionary<string, Type> entityDataNameToType, bool overwrite = false) {
            foreach(KeyValuePair<string,Type> kvp in entityDataNameToType) {
                if(overwrite || !entityDataNameToType.ContainsKey(kvp.Key))
                    entityDataNameToType[kvp.Key] = kvp.Value;
            }
        }

        public static bool GetTypeFromDataName(string entityDataName, out Type type) {
            return entityDataNameToType.TryGetValue(entityDataName, out type);
        }

        public static bool CheckEntityByDataName(Entity entity, string entityDataName) {
            return (entity as patch_Entity).__EntityData is { } ed && ed.Name == entityDataName;
        }

        public static bool CheckEntityByData(Entity entity, Predicate<EntityData> predicate) {
            return (entity as patch_Entity).__EntityData is { } ed && predicate(ed);
        }
        public static Type GetType(string fullname, bool cache = true) {
            if(typeFullNameToTypeCache.ContainsKey(fullname)) { return typeFullNameToTypeCache[fullname]; }
            Type type = FakeAssembly.GetFakeEntryAssembly().GetType(fullname);
            if(type != null) {
                typeFullNameToTypeCache[fullname] = type;
                return type;
            }
            return null;
        }

        /// <summary>
        /// NOT MEANT FOR CODE MODDERS - 
        /// Debug Command method for retrieving the Class name (and mod name it comes from), Class FullName, and CustomEntityAttribute name,
        /// given 1 of them (and a mod name if needed) Useful for mappers.
        /// </summary>
        /// <param name="name">Any of CustomEntityAttribute Name, Class Name, or Class FullName</param>
        /// <param name="modName">Optional: The mod that the class you are searching for is from</param>
        /// <returns></returns>
        internal static string GetTypesFromMod(string name, string modName = null) {
            if (string.IsNullOrWhiteSpace(name))
                return "Takes in a text field of one of the following forms:\n" +
                    ": EntityData Name, e.g. `Everest/FlagTrigger`\n" +
                    ": Class FullName, e.g. `Everest.Entities.FlagTrigger`\n" +
                    ": Class Name + Mod Source, e.g. `FlagTrigger Everest`\n" +
                    "and returns all of the other formats.";
            string EntityDataName = null;
            Type type = null;
            EverestModule mod = null;
            //Check for CustomEntityAttribute first, since that is the main cached item
            if (entityDataNameToType.ContainsKey(name)) {
                EntityDataName = name;
                type = entityDataNameToType[name];
            } else if (name.Contains('/')) { } // Eliminates the large chance of unknown CustomEntityAttribute names
              else if (name.Contains('.')) { // Checks the fullname case for the majority of entities (any that are in a namespace lol)

                if (typeFullNameToTypeCache.ContainsKey(name)) {
                    type = typeFullNameToTypeCache[name];
                } else {
                    try {
                        type = FakeAssembly.GetFakeEntryAssembly().GetType(name);
                        if (type != null)
                            typeFullNameToTypeCache[name] = type;
                    }
                    catch { }
                }
                    
            } else if (modName != null) {
                mod = Everest.Modules.FirstOrDefault(m => m.Metadata.Name == modName);
                if (mod != null && mod is not NullModule && mod is not LuaModule) {
                    Assembly asm = mod.GetType().Assembly;
                    type = asm.GetTypes().FirstOrDefault(t => t.Name == name);
                }
            }
            string ret = null;
            if (type == null) {
                ret = $"Error: the class related to \"{name}\" could not be found. Try checking your spelling";
                if (name != null && modName == null)
                    ret += ", and type the name of the Mod this class is coming from after it.";
                else
                    ret += ".";
                return ret;
            }
            if(mod == null) {
                mod = (EverestModule) type.Assembly.GetTypes().FirstOrDefault(t => typeof(EverestModule).IsAssignableFrom(t)).GetField("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);                
            }
            if (EntityDataName == null) {
                List<string> strings = new List<string>();
                foreach(KeyValuePair<string, Type> kvp in entityDataNameToType) {
                    if(kvp.Value.FullName == type.FullName) {
                        strings.Add(kvp.Key);
                    }
                }
                EntityDataName = string.Join(", ", strings);
            }
            return $"C# Class: {type.Name}, FullName: {type.FullName}\nSource Mod: {mod.Metadata.Name}\nEntityData Names: {EntityDataName}";
        }
    }
}
