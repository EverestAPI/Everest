using Celeste.Mod.Core;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Celeste {
    class patch_OuiFileSelect : OuiFileSelect {

        internal bool startingNewFile;

        [PatchOuiFileSelectSubmenuChecks] // we want to manipulate the orig method with MonoModRules
        public extern IEnumerator orig_Enter(Oui from);
        public new IEnumerator Enter(Oui from) {
            if (!Loaded) {
                int maxSaveFile;

                if (CoreModule.Settings.MaxSaveSlots != null) {
                    maxSaveFile = Math.Max(3, CoreModule.Settings.MaxSaveSlots.Value);

                } else {
                    // first load: we want to check how many slots there are by checking which files exist in the Saves folder.
                    maxSaveFile = 1; // we're adding 2 later, so there will be at least 3 slots.
                    string saveFilePath = patch_UserIO.GetSaveFilePath();
                    if (Directory.Exists(saveFilePath)) {
                        foreach (string filePath in Directory.GetFiles(saveFilePath)) {
                            string fileName = Path.GetFileName(filePath);
                            // is the file named [number].celeste?
                            if (fileName.EndsWith(".celeste") && int.TryParse(fileName.Substring(0, fileName.Length - 8), out int fileIndex)) {
                                maxSaveFile = Math.Max(maxSaveFile, fileIndex);
                            }
                        }
                    }

                    // if 2.celeste exists, slot 3 is the last slot filled, therefore we want 4 slots (2 + 2) to always have the latest one empty.
                    maxSaveFile += 2;
                }

                Slots = new OuiFileSelectSlot[maxSaveFile];
            }

            int slotIndex = 0;
            IEnumerator orig = orig_Enter(from);
            while (orig.MoveNext()) {
                if (orig.Current is float f && f == 0.02f) {
                    // only apply the delay if the slot is on-screen (less than 2 slots away from the selected one).
                    if (Math.Abs(SlotIndex - slotIndex) <= 2) {
                        yield return orig.Current;
                    }
                    slotIndex++;
                } else {
                    yield return orig.Current;
                }
            }
        }

        [PatchOuiFileSelectSubmenuChecks] // we want to manipulate the orig method with MonoModRules
        public extern IEnumerator orig_Leave(Oui next);
        public new IEnumerator Leave(Oui next) {
            int slotIndex = 0;
            IEnumerator orig = orig_Leave(next);
            while (orig.MoveNext()) {
                if (orig.Current is float f && f == 0.02f) {
                    if (next is OuiFileNaming && SlotIndex == slotIndex) {
                        // vanilla moves the file slot at the Y position slot 0 is supposed to be.
                        // ... this doesn't work in our case, since slot 0 might be offscreen.
                        Slots[slotIndex].MoveTo(Slots[slotIndex].IdlePosition.X, 230f);
                    }

                    // only apply the delay if the slot is on-screen (less than 2 slots away from the selected one).
                    if (Math.Abs(SlotIndex - slotIndex) <= 2) {
                        yield return orig.Current;
                    }
                    slotIndex++;
                } else {
                    yield return orig.Current;
                }
            }
        }

#pragma warning disable CS0626 // extern method with no attribute
        public extern void orig_Update();
#pragma warning restore CS0626
        public override void Update() {
            int initialFileIndex = SlotIndex;

            orig_Update();

            if (Focused && !SlotSelected) {
                if (CoreModule.Settings.MenuPageUp.Pressed && SlotIndex > 0) {
                    float startY = Slots[SlotIndex].Y;
                    while (Slots[SlotIndex].Y > startY - 1080f && SlotIndex > 0) {
                        SlotIndex--;
                    }
                    Audio.Play("event:/ui/main/savefile_rollover_up");
                } else if (CoreModule.Settings.MenuPageDown.Pressed && SlotIndex < Slots.Length - 1) {
                    float startY = Slots[SlotIndex].Y;
                    while (Slots[SlotIndex].Y < startY + 1080f && SlotIndex < Slots.Length - 1) {
                        SlotIndex++;
                    }
                    Audio.Play("event:/ui/main/savefile_rollover_down");
                }
            }

            if (SlotIndex != initialFileIndex) {
                // selection moved, so update the Y position of all file slots.
                foreach (OuiFileSelectSlot slot in Slots) {
                    (slot as patch_OuiFileSelectSlot).ScrollTo(slot.IdlePosition.X, slot.IdlePosition.Y);
                }
            }
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the checks for OuiAssistMode to include a check for OuiFileSelectSlot.ISubmenu as well.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchOuiFileSelectSubmenuChecks))]
    class PatchOuiFileSelectSubmenuChecksAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchOuiFileSelectSubmenuChecks(MethodDefinition method, CustomAttribute attrib) {
            TypeDefinition t_ISubmenu = method.Module.GetType("Celeste.OuiFileSelectSlot/ISubmenu");

            // The routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();

            bool found = false;

            ILProcessor il = method.Body.GetILProcessor();
            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count - 4; instri++) {
                if (instrs[instri].OpCode == OpCodes.Brtrue_S
                    && instrs[instri + 1].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 2].OpCode == OpCodes.Ldfld
                    && instrs[instri + 3].MatchIsinst("Celeste.OuiAssistMode")) {
                    // gather some info
                    FieldReference field = (FieldReference) instrs[instri + 2].Operand;
                    Instruction branchTarget = (Instruction) instrs[instri].Operand;

                    // then inject another similar check for ISubmenu
                    instri++;
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, field));
                    instrs.Insert(instri++, il.Create(OpCodes.Isinst, t_ISubmenu));
                    instrs.Insert(instri++, il.Create(OpCodes.Brtrue_S, branchTarget));

                    found = true;

                } else if (instrs[instri].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 1].OpCode == OpCodes.Ldfld
                    && instrs[instri + 2].MatchIsinst("Celeste.OuiAssistMode")
                    && instrs[instri + 3].OpCode == OpCodes.Brfalse_S) {
                    // gather some info
                    FieldReference field = (FieldReference) instrs[instri + 1].Operand;
                    Instruction branchTarget = instrs[instri + 4];

                    // then inject another similar check for ISubmenu
                    instri++;
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, field));
                    instrs.Insert(instri++, il.Create(OpCodes.Isinst, t_ISubmenu));
                    instrs.Insert(instri++, il.Create(OpCodes.Brtrue_S, branchTarget));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));

                    found = true;
                }
            }


            if (!found) {
                throw new Exception("Call to isinst OuiAssistMode not found in " + method.FullName + "!");
            }
        }

    }
}
