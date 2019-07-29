#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_Celeste : Celeste {

        // We're effectively in Celeste, but still need to "expose" private fields to our mod.
        private bool firstLoad;

        public static extern void orig_Main(string[] args);
        public static void Main(string[] args) {
            if (File.Exists("log.txt"))
                File.Delete("log.txt");
            using (Stream fileStream = new FileStream("log.txt", FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
            using (StreamWriter fileWriter = new StreamWriter(fileStream, Console.OutputEncoding))
            using (LogWriter logWriter = new LogWriter {
                STDOUT = Console.Out,
                File = fileWriter
            }) {
                Console.SetOut(logWriter);

                Everest.ParseArgs(args);
                orig_Main(args);

                Everest.Events.Celeste.Shutdown();

                Console.SetOut(logWriter.STDOUT);
                logWriter.STDOUT = null;
            }
        }

        // Patching constructors is ugly.
        public extern void orig_ctor_Celeste();
        [MonoModConstructor]
        [MonoModOriginalName("orig_ctor_Celeste")] // For Everest.Installer
        public void ctor() {
            // Everest.Flags aren't initialized this early.
            if (Environment.GetEnvironmentVariable("EVEREST_HEADLESS") == "1") {
                Instance = this;
                Version = new Version(0, 0, 0, 0);
                Console.WriteLine("CELESTE HEADLESS VIA EVEREST");
            } else {
                orig_ctor_Celeste();
            }
            try {
                Everest.Boot();
            } catch (Exception e) {
                e.LogDetailed();
                /*
                ErrorLog.Write(e);
                ErrorLog.Open();
                */
                throw;
            }
        }

        protected extern void orig_Initialize();
        protected override void Initialize() {
            // Note: You may instinctually call base.Initialize();
            // DON'T! The original method is orig_Initialize
            orig_Initialize();

            Everest.Initialize();
        }

        protected extern void orig_LoadContent();
        protected override void LoadContent() {
            // Note: You may instinctually call base.LoadContent();
            // DON'T! The original method is orig_LoadContent
            bool firstLoad = this.firstLoad;
            orig_LoadContent();

            Everest.Invoke("LoadContent", firstLoad);
        }

        protected override void OnExiting(object sender, EventArgs args) {
            base.OnExiting(sender, args);
            Everest.Events.Celeste.Exiting();
        }

    }
}
