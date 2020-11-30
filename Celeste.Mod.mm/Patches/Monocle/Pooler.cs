using MonoMod;
using System;
using System.Collections.Generic;

namespace Monocle {
    class patch_Pooler : Pooler {
        // this is here to make the compiler recognize that this internal field is indeed in the same assembly.
        internal Dictionary<Type, Queue<Entity>> Pools {
            [MonoModIgnore]
            get;
        }
    }
}
