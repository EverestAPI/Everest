using Celeste.Mod.Core;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.IO;
using System.Linq;

namespace Celeste {
    class patch_OuiFileSelect : OuiFileSelect {
        public float Scroll = 0f;

        [PatchOuiFileSelectSubmenuChecks] // we want to manipulate the orig method with MonoModRules
        [PatchOuiFileSelectEnter]
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

            if (SlotIndex != initialFileIndex) {
                // selection moved, so update the Y position of all file slots.
                foreach (OuiFileSelectSlot slot in Slots) {
                    (slot as patch_OuiFileSelectSlot).ScrollTo(slot.IdlePosition.X, slot.IdlePosition.Y);
                }
            }
        }

        [PatchOuiFileSelectLoadThread]
        [MonoModIgnore]
        private extern void LoadThread();

        private void RemoveSlotsFromScene() {
            Scene.Remove(Slots
                .Where(slot => slot != null)
                // do not remove this line or the game will crash
                .Where(_ => true));
        }

        private void AddSlotsToScene() {
            Scene.Add(Slots);
        }
    }
}
