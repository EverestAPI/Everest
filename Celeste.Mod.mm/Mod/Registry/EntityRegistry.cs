using Celeste.Mod.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Celeste.Mod.Registry;

public static class EntityRegistry {
    private static readonly Dictionary<string, HashSet<Type>> SidToTypes = new();
    private static readonly Dictionary<Type, HashSet<string>> TypeToSids = new();
    
    private static readonly HashSet<Type> EmptyTypeSet = new();
    private static readonly HashSet<string> EmptyStringSet = new();

    /// <summary>
    /// Gets a set of all known C# types associated with the given entity sid.
    /// Might not necessarily be exhaustive, for example if entities from that sid have not been instantiated yet.
    /// </summary>
    public static IReadOnlySet<Type> GetKnownTypesFromSid(string sid) => SidToTypes.GetValueOrDefault(sid) ?? EmptyTypeSet;
    
    /// <summary>
    /// Gets a set of all known sids associated with the given C# type.
    /// Might not necessarily be exhaustive, for example if entities from that type have not been instantiated yet.
    /// </summary>
    public static IReadOnlySet<string> GetKnownSidsFromType(Type type) => TypeToSids.GetValueOrDefault(type) ?? EmptyStringSet;
    
    internal static void RegisterSidToTypeConnection(string sid, Type type) {
        ref var sidToTypeEntry = ref CollectionsMarshal.GetValueRefOrAddDefault(SidToTypes, sid, out _);
        sidToTypeEntry ??= new(1);
        sidToTypeEntry.Add(type);
        
        ref var typeToSidsEntry = ref CollectionsMarshal.GetValueRefOrAddDefault(TypeToSids, type, out _);
        typeToSidsEntry ??= new(1);
        typeToSidsEntry.Add(sid);
    }

    internal static void OnModAssemblyUnload(Assembly asm) {
        var types = asm.GetTypesSafe().ToHashSet();
        foreach (var t in types)
            TypeToSids.Remove(t);
        foreach (var (_, set) in SidToTypes)
            set.RemoveWhere(x => types.Contains(x));
    }
    
    static EntityRegistry() {
        // Register vanilla entities, which do not use the [CustomEntity] attribute.
        // While the same mechanism as the one used for Everest.Events.Level.LoadEntity works for figuring out these relations,
        // this way allows us to know about these relations ahead of time
        RegisterSidToTypeConnection("checkpoint", typeof(Checkpoint));
        RegisterSidToTypeConnection("jumpThru", typeof(JumpthruPlatform));
        RegisterSidToTypeConnection("refill", typeof(Refill));
        RegisterSidToTypeConnection("infiniteStar", typeof(FlyFeather));
        RegisterSidToTypeConnection("strawberry", typeof(Strawberry));
        RegisterSidToTypeConnection("memorialTextController", typeof(Strawberry));
        RegisterSidToTypeConnection("goldenBerry", typeof(Strawberry));
        RegisterSidToTypeConnection("summitgem", typeof(SummitGem));
        RegisterSidToTypeConnection("blackGem", typeof(HeartGem));
        RegisterSidToTypeConnection("dreamHeartGem", typeof(DreamHeartGem));
        RegisterSidToTypeConnection("spring", typeof(Spring));
        RegisterSidToTypeConnection("wallSpringLeft", typeof(Spring));
        RegisterSidToTypeConnection("wallSpringRight", typeof(Spring));
        RegisterSidToTypeConnection("fallingBlock", typeof(FallingBlock));
        RegisterSidToTypeConnection("zipMover", typeof(ZipMover));
        RegisterSidToTypeConnection("crumbleBlock", typeof(CrumblePlatform));
        RegisterSidToTypeConnection("dreamBlock", typeof(DreamBlock));
        RegisterSidToTypeConnection("touchSwitch", typeof(TouchSwitch));
        RegisterSidToTypeConnection("switchGate", typeof(SwitchGate));
        RegisterSidToTypeConnection("negaBlock", typeof(NegaBlock));
        RegisterSidToTypeConnection("key", typeof(Key));
        RegisterSidToTypeConnection("lockBlock", typeof(LockBlock));
        RegisterSidToTypeConnection("movingPlatform", typeof(MovingPlatform));
        RegisterSidToTypeConnection("rotatingPlatforms", typeof(RotatingPlatform));
        RegisterSidToTypeConnection("blockField", typeof(BlockField));
        RegisterSidToTypeConnection("cloud", typeof(Cloud));
        RegisterSidToTypeConnection("booster", typeof(Booster));
        RegisterSidToTypeConnection("moveBlock", typeof(MoveBlock));
        RegisterSidToTypeConnection("light", typeof(PropLight));
        RegisterSidToTypeConnection("switchBlock", typeof(SwapBlock));
        RegisterSidToTypeConnection("swapBlock", typeof(SwapBlock));
        RegisterSidToTypeConnection("dashSwitchH", typeof(DashSwitch));
        RegisterSidToTypeConnection("dashSwitchV", typeof(DashSwitch));
        RegisterSidToTypeConnection("templeGate", typeof(TempleGate));
        RegisterSidToTypeConnection("torch", typeof(Torch));
        RegisterSidToTypeConnection("templeCrackedBlock", typeof(TempleCrackedBlock));
        RegisterSidToTypeConnection("seekerBarrier", typeof(SeekerBarrier));
        RegisterSidToTypeConnection("theoCrystal", typeof(TheoCrystal));
        RegisterSidToTypeConnection("glider", typeof(Glider));
        RegisterSidToTypeConnection("theoCrystalPedestal", typeof(TheoCrystalPedestal));
        RegisterSidToTypeConnection("badelineBoost", typeof(BadelineBoost));
        RegisterSidToTypeConnection("cassette", typeof(Cassette));
        RegisterSidToTypeConnection("cassetteBlock", typeof(CassetteBlock));
        RegisterSidToTypeConnection("wallBooster", typeof(WallBooster));
        RegisterSidToTypeConnection("bounceBlock", typeof(BounceBlock));
        RegisterSidToTypeConnection("coreModeToggle", typeof(CoreModeToggle));
        RegisterSidToTypeConnection("iceBlock", typeof(IceBlock));
        RegisterSidToTypeConnection("fireBarrier", typeof(FireBarrier));
        RegisterSidToTypeConnection("eyebomb", typeof(Puffer));
        RegisterSidToTypeConnection("flingBird", typeof(FlingBird));
        RegisterSidToTypeConnection("flingBirdIntro", typeof(FlingBirdIntro));
        RegisterSidToTypeConnection("birdPath", typeof(BirdPath));
        RegisterSidToTypeConnection("lightningBlock", typeof(LightningBreakerBox));
        RegisterSidToTypeConnection("spikesUp", typeof(Spikes));
        RegisterSidToTypeConnection("spikesDown", typeof(Spikes));
        RegisterSidToTypeConnection("spikesLeft", typeof(Spikes));
        RegisterSidToTypeConnection("spikesRight", typeof(Spikes));
        RegisterSidToTypeConnection("triggerSpikesUp", typeof(TriggerSpikes));
        RegisterSidToTypeConnection("triggerSpikesDown", typeof(TriggerSpikes));
        RegisterSidToTypeConnection("triggerSpikesRight", typeof(TriggerSpikes));
        RegisterSidToTypeConnection("triggerSpikesLeft", typeof(TriggerSpikes));
        RegisterSidToTypeConnection("darkChaser", typeof(BadelineOldsite));
        RegisterSidToTypeConnection("rotateSpinner", typeof(BladeRotateSpinner));
        RegisterSidToTypeConnection("rotateSpinner", typeof(DustRotateSpinner));
        RegisterSidToTypeConnection("rotateSpinner", typeof(StarRotateSpinner));
        RegisterSidToTypeConnection("trackSpinner", typeof(BladeTrackSpinner));
        RegisterSidToTypeConnection("trackSpinner", typeof(StarTrackSpinner));
        RegisterSidToTypeConnection("trackSpinner", typeof(DustTrackSpinner));
        RegisterSidToTypeConnection("spinner", typeof(CrystalStaticSpinner));
        RegisterSidToTypeConnection("sinkingPlatform", typeof(SinkingPlatform));
        RegisterSidToTypeConnection("friendlyGhost", typeof(AngryOshiro));
        RegisterSidToTypeConnection("seeker", typeof(Seeker));
        RegisterSidToTypeConnection("seekerStatue", typeof(SeekerStatue));
        RegisterSidToTypeConnection("slider", typeof(Slider));
        RegisterSidToTypeConnection("templeBigEyeball", typeof(TempleBigEyeball));
        RegisterSidToTypeConnection("crushBlock", typeof(CrushBlock));
        RegisterSidToTypeConnection("bigSpinner", typeof(Bumper));
        RegisterSidToTypeConnection("starJumpBlock", typeof(StarJumpBlock));
        RegisterSidToTypeConnection("floatySpaceBlock", typeof(FloatySpaceBlock));
        RegisterSidToTypeConnection("glassBlock", typeof(GlassBlock));
        RegisterSidToTypeConnection("goldenBlock", typeof(GoldenBlock));
        RegisterSidToTypeConnection("fireBall", typeof(FireBall));
        RegisterSidToTypeConnection("risingLava", typeof(RisingLava));
        RegisterSidToTypeConnection("sandwichLava", typeof(SandwichLava));
        RegisterSidToTypeConnection("killbox", typeof(Killbox));
        RegisterSidToTypeConnection("fakeHeart", typeof(FakeHeart));
        RegisterSidToTypeConnection("lightning", typeof(Lightning));
        RegisterSidToTypeConnection("finalBoss", typeof(FinalBoss));
        RegisterSidToTypeConnection("finalBossFallingBlock", typeof(FallingBlock));
        RegisterSidToTypeConnection("finalBossMovingBlock", typeof(FinalBossMovingBlock));
        RegisterSidToTypeConnection("fakeWall", typeof(FakeWall));
        RegisterSidToTypeConnection("fakeBlock", typeof(FakeWall));
        RegisterSidToTypeConnection("dashBlock", typeof(DashBlock));
        RegisterSidToTypeConnection("invisibleBarrier", typeof(InvisibleBarrier));
        RegisterSidToTypeConnection("exitBlock", typeof(ExitBlock));
        RegisterSidToTypeConnection("conditionBlock", typeof(ExitBlock));
        RegisterSidToTypeConnection("coverupWall", typeof(CoverupWall));
        RegisterSidToTypeConnection("crumbleWallOnRumble", typeof(CrumbleWallOnRumble));
        RegisterSidToTypeConnection("ridgeGate", typeof(RidgeGate));
        RegisterSidToTypeConnection("tentacles", typeof(Tentacles));
        RegisterSidToTypeConnection("starClimbController", typeof(StarClimbGraphicsController));
        RegisterSidToTypeConnection("playerSeeker", typeof(PlayerSeeker));
        RegisterSidToTypeConnection("chaserBarrier", typeof(ChaserBarrier));
        RegisterSidToTypeConnection("introCrusher", typeof(IntroCrusher));
        RegisterSidToTypeConnection("bridge", typeof(Bridge));
        RegisterSidToTypeConnection("bridgeFixed", typeof(BridgeFixed));
        RegisterSidToTypeConnection("bird", typeof(BirdNPC));
        RegisterSidToTypeConnection("introCar", typeof(IntroCar));
        RegisterSidToTypeConnection("memorial", typeof(Memorial));
        RegisterSidToTypeConnection("wire", typeof(Wire));
        RegisterSidToTypeConnection("cobweb", typeof(Cobweb));
        RegisterSidToTypeConnection("lamp", typeof(Lamp));
        RegisterSidToTypeConnection("hanginglamp", typeof(HangingLamp));
        RegisterSidToTypeConnection("hahaha", typeof(Hahaha));
        RegisterSidToTypeConnection("bonfire", typeof(Bonfire));
        RegisterSidToTypeConnection("payphone", typeof(Payphone));
        RegisterSidToTypeConnection("colorSwitch", typeof(ClutterSwitch));
        RegisterSidToTypeConnection("clutterDoor", typeof(ClutterDoor));
        RegisterSidToTypeConnection("dreammirror", typeof(DreamMirror));
        RegisterSidToTypeConnection("resortmirror", typeof(ResortMirror));
        RegisterSidToTypeConnection("towerviewer", typeof(Lookout));
        RegisterSidToTypeConnection("picoconsole", typeof(PicoConsole));
        RegisterSidToTypeConnection("wavedashmachine", typeof(WaveDashTutorialMachine));
        RegisterSidToTypeConnection("yellowBlocks", typeof(ClutterBlockBase));
        RegisterSidToTypeConnection("redBlocks", typeof(ClutterBlockBase));
        RegisterSidToTypeConnection("greenBlocks", typeof(ClutterBlockBase));
        RegisterSidToTypeConnection("oshirodoor", typeof(MrOshiroDoor));
        RegisterSidToTypeConnection("templeMirrorPortal", typeof(TempleMirrorPortal));
        RegisterSidToTypeConnection("reflectionHeartStatue", typeof(ReflectionHeartStatue));
        RegisterSidToTypeConnection("resortRoofEnding", typeof(ResortRoofEnding));
        RegisterSidToTypeConnection("gondola", typeof(Gondola));
        RegisterSidToTypeConnection("birdForsakenCityGem", typeof(ForsakenCitySatellite));
        RegisterSidToTypeConnection("whiteblock", typeof(WhiteBlock));
        RegisterSidToTypeConnection("plateau", typeof(Plateau));
        RegisterSidToTypeConnection("soundSource", typeof(SoundSourceEntity));
        RegisterSidToTypeConnection("templeMirror", typeof(TempleMirror));
        RegisterSidToTypeConnection("templeEye", typeof(TempleEye));
        RegisterSidToTypeConnection("clutterCabinet", typeof(ClutterCabinet));
        RegisterSidToTypeConnection("floatingDebris", typeof(FloatingDebris));
        RegisterSidToTypeConnection("foregroundDebris", typeof(ForegroundDebris));
        RegisterSidToTypeConnection("moonCreature", typeof(MoonCreature));
        RegisterSidToTypeConnection("lightbeam", typeof(LightBeam));
        RegisterSidToTypeConnection("door", typeof(Door));
        RegisterSidToTypeConnection("trapdoor", typeof(Trapdoor));
        RegisterSidToTypeConnection("resortLantern", typeof(ResortLantern));
        RegisterSidToTypeConnection("water", typeof(Water));
        RegisterSidToTypeConnection("waterfall", typeof(WaterFall));
        RegisterSidToTypeConnection("bigWaterfall", typeof(BigWaterfall));
        RegisterSidToTypeConnection("clothesline", typeof(Clothesline));
        RegisterSidToTypeConnection("cliffflag", typeof(CliffFlags));
        RegisterSidToTypeConnection("cliffside_flag", typeof(CliffsideWindFlag));
        RegisterSidToTypeConnection("flutterbird", typeof(FlutterBird));
        RegisterSidToTypeConnection("SoundTest3d", typeof(_3dSoundTest));
        RegisterSidToTypeConnection("SummitBackgroundManager", typeof(AscendManager));
        RegisterSidToTypeConnection("summitGemManager", typeof(SummitGem));
        RegisterSidToTypeConnection("heartGemDoor", typeof(HeartGemDoor));
        RegisterSidToTypeConnection("summitcheckpoint", typeof(SummitCheckpoint));
        RegisterSidToTypeConnection("summitcloud", typeof(SummitCloud));
        RegisterSidToTypeConnection("coreMessage", typeof(CoreMessage));
        RegisterSidToTypeConnection("playbackTutorial", typeof(PlayerPlayback));
        RegisterSidToTypeConnection("playbackBillboard", typeof(PlaybackBillboard));
        RegisterSidToTypeConnection("cutsceneNode", typeof(CutsceneNode));
        RegisterSidToTypeConnection("kevins_pc", typeof(KevinsPC));
        RegisterSidToTypeConnection("powerSourceNumber", typeof(PowerSourceNumber));
        RegisterSidToTypeConnection("npc", typeof(NPC00_Granny));
        RegisterSidToTypeConnection("npc", typeof(NPC01_Theo));
        RegisterSidToTypeConnection("npc", typeof(NPC02_Theo));
        RegisterSidToTypeConnection("npc", typeof(NPC03_Oshiro_Cluttter));
        RegisterSidToTypeConnection("npc", typeof(NPC03_Oshiro_Breakdown));
        RegisterSidToTypeConnection("npc", typeof(NPC03_Oshiro_Hallway2));
        RegisterSidToTypeConnection("npc", typeof(NPC03_Oshiro_Hallway1));
        RegisterSidToTypeConnection("npc", typeof(NPC03_Oshiro_Lobby));
        RegisterSidToTypeConnection("npc", typeof(NPC03_Oshiro_Rooftop));
        RegisterSidToTypeConnection("npc", typeof(NPC03_Oshiro_Suite));
        RegisterSidToTypeConnection("npc", typeof(NPC03_Theo_Escaping));
        RegisterSidToTypeConnection("npc", typeof(NPC03_Theo_Vents));
        RegisterSidToTypeConnection("npc", typeof(NPC04_Theo));
        RegisterSidToTypeConnection("npc", typeof(NPC04_Granny));
        RegisterSidToTypeConnection("npc", typeof(NPC05_Badeline));
        RegisterSidToTypeConnection("npc", typeof(NPC05_Theo_Entrance));
        RegisterSidToTypeConnection("npc", typeof(NPC05_Theo_Mirror));
        RegisterSidToTypeConnection("npc", typeof(NPC06_Granny));
        RegisterSidToTypeConnection("npc", typeof(NPC06_Badeline_Crying));
        RegisterSidToTypeConnection("npc", typeof(NPC06_Granny_Ending));
        RegisterSidToTypeConnection("npc", typeof(NPC06_Theo_Ending));
        RegisterSidToTypeConnection("npc", typeof(NPC06_Theo_Plateau));
        RegisterSidToTypeConnection("npc", typeof(NPC07X_Granny_Ending));
        RegisterSidToTypeConnection("npc", typeof(NPC08_Theo));
        RegisterSidToTypeConnection("npc", typeof(NPC08_Granny));
        RegisterSidToTypeConnection("npc", typeof(NPC09_Granny_Outside));
        RegisterSidToTypeConnection("npc", typeof(NPC09_Granny_Inside));
        RegisterSidToTypeConnection("npc", typeof(NPC10_Gravestone));
        RegisterSidToTypeConnection("eventTrigger", typeof(EventTrigger));
        RegisterSidToTypeConnection("musicFadeTrigger", typeof(MusicFadeTrigger));
        RegisterSidToTypeConnection("musicTrigger", typeof(MusicTrigger));
        RegisterSidToTypeConnection("altMusicTrigger", typeof(AltMusicTrigger));
        RegisterSidToTypeConnection("cameraOffsetTrigger", typeof(CameraOffsetTrigger));
        RegisterSidToTypeConnection("lightFadeTrigger", typeof(LightFadeTrigger));
        RegisterSidToTypeConnection("bloomFadeTrigger", typeof(BloomFadeTrigger));
        RegisterSidToTypeConnection("cameraTargetTrigger", typeof(CameraTargetTrigger));
        RegisterSidToTypeConnection("cameraAdvanceTargetTrigger", typeof(CameraAdvanceTargetTrigger));
        RegisterSidToTypeConnection("respawnTargetTrigger", typeof(RespawnTargetTrigger));
        RegisterSidToTypeConnection("changeRespawnTrigger", typeof(ChangeRespawnTrigger));
        RegisterSidToTypeConnection("windTrigger", typeof(WindTrigger));
        RegisterSidToTypeConnection("windAttackTrigger", typeof(WindAttackTrigger));
        RegisterSidToTypeConnection("minitextboxTrigger", typeof(MiniTextboxTrigger));
        RegisterSidToTypeConnection("oshiroTrigger", typeof(OshiroTrigger));
        RegisterSidToTypeConnection("interactTrigger", typeof(InteractTrigger));
        RegisterSidToTypeConnection("checkpointBlockerTrigger", typeof(CheckpointBlockerTrigger));
        RegisterSidToTypeConnection("lookoutBlocker", typeof(LookoutBlocker));
        RegisterSidToTypeConnection("stopBoostTrigger", typeof(StopBoostTrigger));
        RegisterSidToTypeConnection("noRefillTrigger", typeof(NoRefillTrigger));
        RegisterSidToTypeConnection("ambienceParamTrigger", typeof(AmbienceParamTrigger));
        RegisterSidToTypeConnection("creditsTrigger", typeof(CreditsTrigger));
        RegisterSidToTypeConnection("goldenBerryCollectTrigger", typeof(GoldBerryCollectTrigger));
        RegisterSidToTypeConnection("moonGlitchBackgroundTrigger", typeof(MoonGlitchBackgroundTrigger));
        RegisterSidToTypeConnection("blackholeStrength", typeof(BlackholeStrengthTrigger));
        RegisterSidToTypeConnection("rumbleTrigger", typeof(RumbleTrigger));
        RegisterSidToTypeConnection("birdPathTrigger", typeof(BirdPathTrigger));
        RegisterSidToTypeConnection("spawnFacingTrigger", typeof(SpawnFacingTrigger));
        RegisterSidToTypeConnection("detachFollowersTrigger", typeof(DetachStrawberryTrigger));
    }
}