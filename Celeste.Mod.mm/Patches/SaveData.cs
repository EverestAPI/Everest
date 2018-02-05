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

namespace Celeste {
    class patch_SaveData : SaveData {

        public extern void orig_AfterInitialize();
        public new void AfterInitialize() {
            orig_AfterInitialize();
            Everest.Invoke("LoadSaveData", FileSlot);
        }

        public extern void orig_BeforeSave();
        public new void BeforeSave() {
            orig_BeforeSave();
            Everest.Invoke("SaveSaveData", FileSlot);
        }

    }
}
