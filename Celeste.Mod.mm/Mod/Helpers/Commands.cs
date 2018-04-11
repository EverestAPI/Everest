using Monocle;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Helpers {
    internal static class Commands {

        [Command("q", "hides the command line")]
        public static void Hide() {
            Engine.Commands.Open = false;
        }

    }
}
