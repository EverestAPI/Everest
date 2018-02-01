#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_Celeste : Celeste {

        public static extern void orig_Main(string[] args);
        public static void Main(string[] args) {
            Everest.ParseArgs(args);

            orig_Main(args);
        }

        protected extern void orig_Initialize();
        protected override void Initialize() {
            orig_Initialize();

            Everest.Initialize();
        }

    }
}
