
namespace Celeste.Mod.UI {
    /// <summary>
    /// Just a handy wrapper on OuiGenericMenu for OuiFileSelectSlot submenus specifically.
    /// </summary>
    public abstract class OuiFileSelectSlotSubmenu : OuiGenericMenu, patch_OuiFileSelectSlot.ISubmenu {
        public static void Goto<T>(OuiFileSelectSlot slot, EverestModuleSaveData modSaveData, bool fileExists)
            where T : OuiFileSelectSlotSubmenu {

            slot.Assisting = true; // same animation as assist mode.
            Goto<T>(overworld => overworld.Goto<OuiFileSelect>(), slot, modSaveData, fileExists);
        }

        protected abstract void addOptionsToMenu(TextMenu menu, OuiFileSelectSlot slot, EverestModuleSaveData modSaveData, bool fileExists);

        protected override void addOptionsToMenu(patch_TextMenu menu) {
            addOptionsToMenu(menu, parameters[0] as OuiFileSelectSlot, parameters[1] as EverestModuleSaveData, (bool) parameters[2]);
        }
    }
}
