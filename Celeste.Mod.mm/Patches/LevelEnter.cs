#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod;
using MonoMod;

namespace Celeste {
    class patch_LevelEnter {

        extern public static void orig_Go(Session session, bool fromSaveData);

        public static void Go(Session session, bool fromSaveData) {
            orig_Go(session, fromSaveData);
            Everest.Events.LevelEnter.Go(session, fromSaveData);
        }
    }
}
