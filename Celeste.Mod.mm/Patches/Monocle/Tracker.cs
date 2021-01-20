#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Helpers;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Monocle {
    /// <summary>
    /// When applied on an entity or component, this attribute makes the entity tracked the same way as another entity or component.
    /// </summary>
    public class TrackedAsAttribute : Attribute {
        public Type TrackedAsType;
        public bool Inherited;

        /// <summary>
        /// Makes this entity/component tracked the same way as another entity/component.<br/>
        /// It can then be accessed through <see cref="Tracker.GetEntities{T}"/> or <see cref="Tracker.GetComponents{T}"/> with the generic param of <paramref name="trackedAsType"/>.
        /// </summary>
        /// <param name="trackedAsType">Type to track this entity/component as.</param>
        public TrackedAsAttribute(Type trackedAsType) {
            TrackedAsType = trackedAsType;
        }

        /// <inheritdoc cref="TrackedAsAttribute(Type)"/>
        /// <param name="inherited">Whether all child classes should also be tracked as <paramref name="trackedAsType"/>.</param>
        public TrackedAsAttribute(Type trackedAsType, bool inherited = false) {
            TrackedAsType = trackedAsType;
            Inherited = inherited;
        }
    }

    class patch_Tracker : Tracker {
        [MonoModIgnore]
        public static extern List<Type> GetSubclasses(Type type);

        public static extern void orig_Initialize();
        public new static void Initialize() {
            orig_Initialize();

            // search for entities with [TrackedAs]
            Type[] types = FakeAssembly.GetFakeEntryAssembly().GetTypesSafe();
            foreach (Type type in types) {
                object[] customAttributes = type.GetCustomAttributes(typeof(TrackedAsAttribute), inherit: false);
                foreach (object customAttribute in customAttributes) {
                    TrackedAsAttribute trackedAs = customAttribute as TrackedAsAttribute;
                    Type trackedAsType = trackedAs.TrackedAsType;
                    bool inherited = trackedAs.Inherited;
                    if (typeof(Entity).IsAssignableFrom(type)) {
                        if (!type.IsAbstract) {
                            // this is an entity. copy the registered types for the target entity
                            if (!TrackedEntityTypes.ContainsKey(type)) {
                                TrackedEntityTypes.Add(type, new List<Type>());
                            }
                            TrackedEntityTypes[type].AddRange(TrackedEntityTypes.TryGetValue(trackedAsType, out List<Type> list) ? list : new List<Type>());
                            TrackedEntityTypes[type] = TrackedEntityTypes[type].Distinct().ToList();
                        }
                        if (inherited) {
                            // do the same for subclasses
                            foreach (Type subclass in GetSubclasses(type)) {
                                if (!subclass.IsAbstract) {
                                    if (!TrackedEntityTypes.ContainsKey(subclass))
                                        TrackedEntityTypes.Add(subclass, new List<Type>());
                                    TrackedEntityTypes[subclass].AddRange(TrackedEntityTypes.TryGetValue(trackedAsType, out List<Type> list) ? list : new List<Type>());
                                    TrackedEntityTypes[subclass] = TrackedEntityTypes[subclass].Distinct().ToList();
                                }
                            }
                        }
                    } else if (typeof(Component).IsAssignableFrom(type)) {
                        if (!type.IsAbstract) {
                            // this is an component. copy the registered types for the target component
                            if (!TrackedComponentTypes.ContainsKey(type)) {
                                TrackedComponentTypes.Add(type, new List<Type>());
                            }
                            TrackedComponentTypes[type].AddRange(TrackedComponentTypes.TryGetValue(trackedAsType, out List<Type> list) ? list : new List<Type>());
                            TrackedComponentTypes[type] = TrackedComponentTypes[type].Distinct().ToList();
                        }
                        if (inherited) {
                            // do the same for subclasses
                            foreach (Type subclass in GetSubclasses(type)) {
                                if (!subclass.IsAbstract) {
                                    if (!TrackedComponentTypes.ContainsKey(subclass))
                                        TrackedComponentTypes.Add(subclass, new List<Type>());
                                    TrackedComponentTypes[subclass].AddRange(TrackedComponentTypes.TryGetValue(trackedAsType, out List<Type> list) ? list : new List<Type>());
                                    TrackedComponentTypes[subclass] = TrackedComponentTypes[subclass].Distinct().ToList();
                                }
                            }
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
