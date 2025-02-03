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
        /// <param name="trackedAsType">Type to track this entity/component as.</param>
        /// <param name="inherited">Whether all child classes should also be tracked as <paramref name="trackedAsType"/>.</param>
        public TrackedAsAttribute(Type trackedAsType, bool inherited = false) {
            TrackedAsType = trackedAsType;
            Inherited = inherited;
        }
    }

    class patch_Tracker : Tracker {
        // A temporary cache to store the results of FakeAssembly.GetFakeEntryAssemblies
        // Set to null outside of Initialize.
        private static Type[] _temporaryAllTypes;

        private static Type[] GetAllTypesUncached() => FakeAssembly.GetFakeEntryAssembly().GetTypesSafe();

        private static int TrackedTypeVersion;

        private int currentVersion;

        public extern void orig_ctor();

        [MonoModConstructor]
        public void ctor() {
            orig_ctor();
            currentVersion = TrackedTypeVersion;
        }

        [MonoModReplace]
        private static List<Type> GetSubclasses(Type type) {
            bool shouldNullOutCache = _temporaryAllTypes is null;
            _temporaryAllTypes ??= GetAllTypesUncached();

            List<Type> subclasses = new();
            foreach (Type otherType in _temporaryAllTypes) {
                if (type != otherType && type.IsAssignableFrom(otherType))
                    subclasses.Add(otherType);
            }

            // This method got called outside of Initialize, so we can't rely on it clearing out the cache.
            // Let's do that now instead.
            if (shouldNullOutCache)
                _temporaryAllTypes = null;

            return subclasses;
        }

        public static extern void orig_Initialize();
        public new static void Initialize() {
            _temporaryAllTypes = GetAllTypesUncached();

            orig_Initialize();

            // search for entities with [TrackedAs]
            int oldVersion = TrackedTypeVersion;
            foreach (Type type in _temporaryAllTypes) {
                foreach (TrackedAsAttribute trackedAs in type.GetCustomAttributes(typeof(TrackedAsAttribute), inherit: false).Cast<TrackedAsAttribute>()) {
                    AddTypeToTracker(type, trackedAs.TrackedAsType, trackedAs.Inherited);
                }
            }
            TrackedTypeVersion = oldVersion;
            // don't hold references to all the types anymore
            _temporaryAllTypes = null;
        }

        /// <summary>
        /// Dynamically add a new type to the active scene's tracker. The <paramref name="type"/> can also be TrackedAs if <paramref name="trackedAs"/> is provided.
        /// If <paramref name="inheritAll"/> is true, all subtypes of <paramref name="type"/> will be tracked under it.
        /// Call <seealso cref="AddSpecificType(Type, Type, Dictionary{Type, List{Type}})"/> to add the scene's entities to the tracker after adding the type.
        /// </summary>
        /// <param name="type">The type to add to the Tracker</param>
        /// <param name="trackedAs">If the type should be TrackedAs</param>
        /// <param name="inheritAll">If all subtypes of <paramref name="type"/> should also be tracked under it</param>
        public static void AddTypeToTracker(Type type, Type trackedAs = null, bool inheritAll = false) {
            AddTypeToTracker(type, trackedAs, inheritAll ? GetSubclasses(type).ToArray() : Array.Empty<Type>());
        }

        /// <summary>
        /// Dynamically add a new type to the active scene's tracker. The <paramref name="type"/> can also be TrackedAs if <paramref name="trackedAs"/> is provided.
        /// Any <paramref name="subtypes"/> passed will also be tracked under <paramref name="type"/>.
        /// Call <seealso cref="AddSpecificType(Type, Type, Dictionary{Type, List{Type}})"/> to add the scene's entities to the tracker after adding the type.
        /// </summary>
        /// <param name="type">The type to add to the Tracker</param>
        /// <param name="trackedAs">If the type should be TrackedAs</param>
        /// <param name="subtypes">Any subtypes that should be tracked under <paramref name="type"/></param>
        public static void AddTypeToTracker(Type type, Type trackedAs = null, params Type[] subtypes) {
            Type trackedAsType = trackedAs != null && trackedAs.IsAssignableFrom(type) ? trackedAs : type;
            bool? canTrack = typeof(Entity).IsAssignableFrom(type) ? true : typeof(Component).IsAssignableFrom(type) ? false : null;
            if (canTrack is not bool trackedEntity) {
                // this is neither an entity nor a component. Help!
                throw new Exception("Type '" + type.Name + "' cannot be Tracked" + (trackedAsType != type ? "As" : "") + " because it does not derive from Entity or Component");
            }
            bool updated = false;
            // copy the registered types for the target type
            (trackedEntity ? StoredEntityTypes : StoredComponentTypes).Add(type);
            Dictionary<Type, List<Type>> tracked = trackedEntity ? TrackedEntityTypes : TrackedComponentTypes;
            if (AddSpecificType(type, trackedAsType, tracked)) {
                updated = true;
            }
            // do the same for subclasses
            foreach (Type subtype in subtypes) {
                if (trackedAsType.IsAssignableFrom(subtype) && AddSpecificType(subtype, trackedAsType, tracked)) {
                    updated = true;
                }
            }
            if (updated) {
                TrackedTypeVersion++;
            }
        }

        private static bool AddSpecificType(Type type, Type trackedAsType, Dictionary<Type, List<Type>> tracked) {
            if (type.IsAbstract) {
                return false;
            }
            if (!tracked.TryGetValue(type, out List<Type> value)) {
                value = new List<Type>();
                tracked.Add(type, value);
            }
            int cnt = value.Count;
            value.AddRange(tracked.TryGetValue(trackedAsType, out List<Type> list) ? list : new List<Type>());
            List<Type> result = tracked[type] = value.Distinct().ToList();
            return cnt != result.Count;
        }

        /// <summary>
        /// Ensures the <paramref name="scene"/>'s tracker contains all entities of all tracked Types from the <paramref name="scene"/>.
        /// Must be called if a type is added to the tracker manually and if the <paramref name="scene"/>'s Tracker isn't refreshed.
        /// If called back to back without a type added to the Tracker, it won't go through again, for performance.
        /// <paramref name="force"/> will make ensure the Refresh happens, even if run back to back.
        /// Only the <paramref name="scene"/>'s Tracker's refreshed state is changed.
        /// If <paramref name="scene"/> is null, it will default to Engine.Scene.
        /// </summary>
        public static void Refresh(Scene scene = null, bool force = false) {
            Scene sceneUpdate = scene ?? Engine.Scene;
            if ((sceneUpdate.Tracker as patch_Tracker).currentVersion >= TrackedTypeVersion && !force) {
                return;
            }
            (sceneUpdate.Tracker as patch_Tracker).currentVersion = TrackedTypeVersion;
            foreach (Type entityType in StoredEntityTypes) {
                if (!sceneUpdate.Tracker.Entities.ContainsKey(entityType)) {
                    sceneUpdate.Tracker.Entities.Add(entityType, new List<Entity>());
                }
            }
            foreach (Type componentType in StoredComponentTypes) {
                if (!sceneUpdate.Tracker.Components.ContainsKey(componentType)) {
                    sceneUpdate.Tracker.Components.Add(componentType, new List<Component>());
                }
            }
            foreach (Entity entity in sceneUpdate.Entities) {
                foreach (Component component in entity.Components) {
                    Type componentType = component.GetType();
                    if (!TrackedComponentTypes.TryGetValue(componentType, out List<Type> componentTypes)
                        || sceneUpdate.Tracker.Components[componentType].Contains(component)) {
                        continue;
                    }
                    foreach (Type trackedType in componentTypes) {
                        sceneUpdate.Tracker.Components[trackedType].Add(component);
                    }
                }
                Type entityType = entity.GetType();
                if (!TrackedEntityTypes.TryGetValue(entityType, out List<Type> entityTypes)
                    || sceneUpdate.Tracker.Entities[entityType].Contains(entity)) {
                    continue;
                }
                foreach (Type trackedType in entityTypes) {
                    sceneUpdate.Tracker.Entities[trackedType].Add(entity);
                }
            }
        }
    }
}
