using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Monocle;

namespace Celeste.Mod.Entities {

    /// <summary>
    /// All PreUpdateComponents will run the code inside PreUpdate before the Entity runs its Update call.
    /// </summary>
    public abstract class PreUpdateComponent : Component {
        public PreUpdateComponent(bool active, bool visible):base(active, visible) {

        }

        public abstract void PreUpdate();
    }
}
