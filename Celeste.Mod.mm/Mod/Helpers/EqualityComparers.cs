using System.Collections.Generic;

namespace Celeste.Mod.Helpers {
    public class AlwaysEqual<T> : EqualityComparer<T> {
        public override bool Equals(T x, T y) => true;

        public override int GetHashCode(T obj) => 0;
    }
}