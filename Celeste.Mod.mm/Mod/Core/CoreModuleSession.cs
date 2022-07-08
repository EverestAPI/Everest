using System.Collections.Generic;

namespace Celeste.Mod.Core {
    public class CoreModuleSession : EverestModuleSession {

        public int WhateverElseCount { get; set; } = 1337;

        public HashSet<string> AttachedDecals { get; set; } = new HashSet<string>();

    }
}
