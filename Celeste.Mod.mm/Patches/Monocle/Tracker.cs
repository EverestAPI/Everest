using Celeste.Mod.Helpers;
using System;
using System.Collections.Generic;

namespace Monocle {
    /// <summary>
    /// When applied on an entity, this attribute makes the entity tracked the same way as another entity.
    /// </summary>
    public class TrackedAsAttribute : Attribute {
        public Type TrackedAsType;

        public TrackedAsAttribute(Type trackedAsType) {
            TrackedAsType = trackedAsType;
        }
    }

    class patch_Tracker : Tracker {
#pragma warning disable CS0626 // method, operator or getter is tagged external and has no attribute
        public static extern void orig_Initialize();
#pragma warning restore CS0626

        public new static void Initialize() {
            orig_Initialize();

            // search for entities with [TrackedAs]
            Type[] types = FakeAssembly.GetFakeEntryAssembly().GetTypes();
            foreach (Type type in types) {
                object[] customAttributes = type.GetCustomAttributes(typeof(TrackedAsAttribute), inherit: false);
                foreach (object customAttribute in customAttributes) {
                    Type trackedAsType = (customAttribute as TrackedAsAttribute).TrackedAsType;
                    if (typeof(Entity).IsAssignableFrom(type)) {
                        if (!type.IsAbstract) {
                            // this is an entity. copy the registered types for the target entity
                            if (!TrackedEntityTypes.ContainsKey(type)) {
                                TrackedEntityTypes.Add(type, new List<Type>());
                            }
                            TrackedEntityTypes[type].AddRange(TrackedEntityTypes.TryGetValue(trackedAsType, out List<Type> list) ? list : new List<Type>());
                        }
                    } else if (typeof(Component).IsAssignableFrom(type)) {
                        if (!type.IsAbstract) {
                            // this is an component. copy the registered types for the target component
                            if (!TrackedComponentTypes.ContainsKey(type)) {
                                TrackedComponentTypes.Add(type, new List<Type>());
                            }
                            TrackedComponentTypes[type].AddRange(TrackedComponentTypes.TryGetValue(trackedAsType, out List<Type> list) ? list : new List<Type>());
                        }
                    } else {
                        // this is neither an entity nor a component. Help!
                        throw new Exception("Type '" + type.Name + "' cannot be TrackedAs because it does not derive from Entity or Component");
                    }
                }
            }
        }
    }
}
