#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
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
                throw;
            }
        }

        protected extern void orig_Initialize();
        protected override void Initialize() {
            // Note: You may instinctually call base.Initialize();
            // DON'T! The original method is orig_Initialize
            orig_Initialize();
            Everest.Invoke("Initialize");
        }

        protected override void OnExiting(object sender, EventArgs args) {
            base.OnExiting(sender, args);
            Everest.Events.Celeste.Exiting();
        }

    }
}
