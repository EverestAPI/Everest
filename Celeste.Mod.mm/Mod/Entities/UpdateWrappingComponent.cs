using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Monocle;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// A class that adds update functions before any of the parent entity update and after all of the parent entity update is called.
    /// </summary>
    public class UpdateWrappingComponent : Component {

        public Action PreUpdate;
        public Action PostUpdate;

        public UpdateWrappingComponent(Action preUpdate, Action postUpdate, bool active, bool visible) : base(active, visible) {
            PreUpdate = preUpdate;
            PostUpdate = postUpdate;
        }
    }
}
