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

        public Action<Entity> PreUpdate;
        public Action<Entity> PostUpdate;

        public UpdateWrappingComponent(Action<Entity> preUpdate, Action<Entity> postUpdate) : base(false, false) {
            PreUpdate = preUpdate;
            PostUpdate = postUpdate;
        }
    }
}
