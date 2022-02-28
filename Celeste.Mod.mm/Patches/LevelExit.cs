#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0414 // The field is assigned but its value is never used

using System;
using Celeste.Mod;
using Celeste.Mod.Meta;
using Monocle;
using MonoMod;
using System.Collections;
using System.IO;
using System.Xml;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_LevelExit : LevelExit {

        // We're effectively in LevelExit, but still need to "expose" private fields to our mod.
        private Session session;
        private XmlElement completeXml;
        private Atlas completeAtlas;
        private bool completeLoaded;

        private MapMetaCompleteScreen completeMeta;

        public patch_LevelExit(Mode mode, Session session, HiresSnow snow = null)
            : base(mode, session, snow) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(Mode mode, Session session, HiresSnow snow = null);
        [MonoModConstructor]
        public void ctor(Mode mode, Session session, HiresSnow snow = null) {
            // Restore to metadata of A-Side.
            AreaData.Get(session).RestoreASideAreaData();

            orig_ctor(mode, session, snow);
            Everest.Events.Level.Exit(Engine.Scene as Level, this, mode, session, snow);
        }

        [MonoModReplace]
        private void LoadCompleteThread() {
            AreaData area = AreaData.Get(session);

            if ((completeMeta = area.GetMeta()?.CompleteScreen) != null && completeMeta.Atlas != null) {
                completeAtlas = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", completeMeta.Atlas), Atlas.AtlasDataFormat.PackerNoAtlas);

            } else if ((completeXml = area.CompleteScreenXml) != null && completeXml.HasAttr("atlas")) {
                completeAtlas = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", completeXml.Attr("atlas")), Atlas.AtlasDataFormat.PackerNoAtlas);
            }

            completeLoaded = true;
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchLevelExitRoutine] // ... except for slapping an additional parameter to / updating newobj AreaComplete
        private extern IEnumerator Routine();

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchAreaCompleteMusic] // ... except for manipulating the method via MonoModRules
        private extern new void Begin();

        // returns true if there is custom music, false otherwise.
        private bool playCustomCompleteScreenMusic() {
            string[] completeScreenMusic = AreaData.Get(session.Area)?.GetMeta()?.CompleteScreen?.MusicBySide;
            if (completeScreenMusic != null && completeScreenMusic.Length > (int) session.Area.Mode) {
                Audio.SetMusic(completeScreenMusic[(int) session.Area.Mode]);
                return true;
            }
            return false;
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Slap a ldfld completeMeta right before newobj AreaComplete
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.RegisterLevelExitRoutine))]
    class PatchLevelExitRoutineAttribute : Attribute { }

    /// <summary>
    /// Patches LevelExit.Begin to make the endscreen music customizable.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchAreaCompleteMusic))]
    class PatchAreaCompleteMusicAttribute : Attribute { }

    static partial class MonoModRules {

        public static void RegisterLevelExitRoutine(MethodDefinition method, CustomAttribute attrib) {
            // Register it. Don't patch it directly as we require an explicit patching order.
            LevelExitRoutines.Add(method);
        }

        public static void PatchLevelExitRoutine(MethodDefinition method) {
            FieldDefinition f_completeMeta = method.DeclaringType.FindField("completeMeta");

            // The level exit routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();
            FieldDefinition f_this = method.DeclaringType.FindField("<>4__this");

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];
                MethodReference calling = instr.Operand as MethodReference;
                string callingID = calling?.GetID();

                // The original AreaComplete .ctor has been modified to contain an extra parameter.
                // For safety, check against both signatures.
                if (instr.OpCode != OpCodes.Newobj ||
                    (callingID != "System.Void Celeste.AreaComplete::.ctor(Celeste.Session,System.Xml.XmlElement,Monocle.Atlas,Celeste.HiresSnow)" &&
                        callingID != "System.Void Celeste.AreaComplete::.ctor(Celeste.Session,System.Xml.XmlElement,Monocle.Atlas,Celeste.HiresSnow,Celeste.Mod.Meta.MapMetaCompleteScreen)")
                ) {
                    continue;
                }

                // For safety, replace the .ctor call if the new .ctor exists already.
                instr.Operand = calling.DeclaringType.Resolve().FindMethod("System.Void Celeste.AreaComplete::.ctor(Celeste.Session,System.Xml.XmlElement,Monocle.Atlas,Celeste.HiresSnow,Celeste.Mod.Meta.MapMetaCompleteScreen)") ?? instr.Operand;

                instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));

                if (f_this != null) {
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_this));
                }

                instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_completeMeta));
            }
        }

        public static void PatchAreaCompleteMusic(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_playCustomCompleteScreenMusic = context.Method.DeclaringType.FindMethod("System.Boolean playCustomCompleteScreenMusic()");

            ILCursor cursor = new ILCursor(context);

            // we want to inject code just after RunThread.Start that calls playCustomCompleteScreenMusic(),
            // and sends execution to Audio.SetAmbience(null) if it returned true (skipping over the vanilla code playing endscreen music).
            cursor.GotoNext(MoveType.After, instr => instr.MatchCall("Celeste.RunThread", "Start"));
            Instruction breakTarget = cursor.Clone().GotoNext(
                instr => instr.MatchLdnull(),
                instr => instr.OpCode == OpCodes.Ldc_I4_1,
                instr => instr.MatchCall("Celeste.Audio", "SetAmbience")
            ).Next;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Call, m_playCustomCompleteScreenMusic);
            cursor.Emit(OpCodes.Brtrue_S, breakTarget);
        }

    }
}
