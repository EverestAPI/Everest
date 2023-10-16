using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod {
    public static class TrackerDictionaryHelper {

        public class TypeNameEqualityComparer : IEqualityComparer<Type> {
            public bool Equals(Type x, Type y) {
                return (x == y) || x.FullName.Equals(y.FullName, StringComparison.Ordinal);
            }

            public int GetHashCode(Type type) => type.FullName.GetHashCode();
        }

        public static Dictionary<Type, List<Entity>> MakeEqualityComparerDictionaryForEntityTracker(int capacity)
            => new Dictionary<Type, List<Entity>>(capacity, new TypeNameEqualityComparer());

        public static Dictionary<Type, List<Component>> MakeEqualityComparerDictionaryForComponentTracker(int capacity)
            => new Dictionary<Type, List<Component>>(capacity, new TypeNameEqualityComparer());

    }
}