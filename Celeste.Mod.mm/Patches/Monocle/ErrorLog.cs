#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
using MonoMod;
using MonoMod.Utils;
using System;

namespace Monocle {
    class patch_ErrorLog {

        public static extern void orig_Write(Exception e);
        public static void Write(Exception e) {
            e.LogDetailed();
            Everest.LogDetours();
            orig_Write(e);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchErrorLogWrite] // ... except for manually manipulating the method via MonoModRules
        public static extern void Write(string str);

        public static extern void orig_Open();
        public static void Open() {
            if (Environment.GetEnvironmentVariable("EVEREST_NO_ERRORLOG_ON_CRASH") != "1" && CoreModule.Settings.OpenErrorLogOnCrash)
                orig_Open();
        }

    }
}
