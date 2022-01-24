using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Entities {

    /// <summary>
    /// All Components added to an Entity that inherit this interface will run the code inside PreUpdate before the Entity runs its Update call.
    /// </summary>
    public interface IPreUpdateComponent {
        void PreUpdate();
    }
}
