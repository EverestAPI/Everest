#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Celeste {
    static class patch_UserIO {

        private static extern string orig_GetSavePath();
        private static string GetSavePath() {
            string fallback = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Saves");
            try {
                return orig_GetSavePath();
            } catch (NotSupportedException) {
                return fallback;
            }
        }

    }
}
