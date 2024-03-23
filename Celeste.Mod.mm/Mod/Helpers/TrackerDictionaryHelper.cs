using Monocle;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Celeste.Mod {
    public static class TrackerDictionaryHelper {

        public class TypeNameEqualityComparer : IEqualityComparer<Type> {
            public bool Equals(Type x, Type y) {
                return (x == y) || x.FullName.Equals(y.FullName, StringComparison.Ordinal);
            }

            public int GetHashCode(Type type) => type.FullName.GetHashCode();
        }

        public static Dictionary<Type, List<Entity>> MakeDictionaryForEntityTracker(int capacity) {
            if (Debugger.IsAttached)
                return new Dictionary<Type, List<Entity>>(capacity, new TypeNameEqualityComparer());

            return new Dictionary<Type, List<Entity>>(capacity);
        }

        public static Dictionary<Type, List<Component>> MakeDictionaryForComponentTracker(int capacity) {
            if (Debugger.IsAttached)
                return new Dictionary<Type, List<Component>>(capacity, new TypeNameEqualityComparer());

            return new Dictionary<Type, List<Component>>(capacity);
        }

    }
}