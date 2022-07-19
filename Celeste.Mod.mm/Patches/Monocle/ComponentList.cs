using MonoMod;
using System.Collections.Generic;

namespace Monocle {
    class patch_ComponentList {

        [MonoModIgnore]
        public extern void Remove(IEnumerable<Component> components);
        [MonoModIgnore]
        public extern IEnumerable<T> GetAll<T>();

        [MonoModReplace]
        public void RemoveAll<T>() where T : Component {
            Remove(new List<T>(GetAll<T>()));
        }
    }
}