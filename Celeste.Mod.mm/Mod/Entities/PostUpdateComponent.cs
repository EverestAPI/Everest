using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Monocle;

namespace Celeste.Mod.Entities {

    /// <summary>
    /// All PreUpdateComponents will run the code inside PostUpdate after the Entity completes running its Update call.
    /// </summary>
    public abstract class PostUpdateComponent : Component {

        public PostUpdateComponent(bool active, bool visible) : base(active, visible) {

        }
        public abstract void PostUpdate();
    }
}
