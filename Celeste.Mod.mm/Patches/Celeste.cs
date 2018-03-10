#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
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
            using (Stream fileStream = File.OpenWrite("log.txt"))
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
        public void ctor_Celeste() {
            orig_ctor_Celeste();
            try {
                Everest.Boot();
            } catch (Exception e) {
                e.LogDetailed();
                ErrorLog.Write(e);
                ErrorLog.Open();
                throw;
            }
        }

        protected extern void orig_Initialize();
        protected override void Initialize() {
            // Note: You may instinctually call base.Initialize();
            // DON'T! The original method is orig_Initialize
            orig_Initialize();

            // Initialize misc stuff.
            TextInput.Initialize(this);

            Everest.Invoke("Initialize");
        }

        protected extern void orig_LoadContent();
        protected override void LoadContent() {
            // Note: You may instinctually call base.LoadContent();
            // DON'T! The original method is orig_LoadContent
            bool firstLoad = this.firstLoad;
            orig_LoadContent();

            if (firstLoad) {
                SubHudRenderer.Buffer = VirtualContent.CreateRenderTarget("subhud-target", 1922, 1082);
            }

            Everest.Invoke("LoadContent");
            Everest.Invoke("LoadContent", firstLoad);
        }

        protected override void OnExiting(object sender, EventArgs args) {
            base.OnExiting(sender, args);
            Everest.Events.Celeste.Exiting();
        }

    }
}
