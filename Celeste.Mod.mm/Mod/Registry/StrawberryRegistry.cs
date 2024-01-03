using Celeste.Mod.Entities;
using Monocle;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Celeste.Mod {
    /// <summary>
    /// Allows mods to register their own strawberry-type collectible objects.<para />
    /// Registered strawberries can be tracked or secret, and will attempt to autocollect when appropriate e.g. level end.
    /// </summary>
    public static class StrawberryRegistry {
        private static HashSet<RegisteredBerry> registeredBerries = new HashSet<RegisteredBerry>() {
            new RegisteredBerry(typeof(Strawberry), "strawberry", true, false), // red berries
            new RegisteredBerry(typeof(Strawberry), "goldenBerry", false, true), // golden berries
            new RegisteredBerry(typeof(Strawberry), "memorialTextController", false, true) // dashless golden berry
        };

        // Caches
        private static ReadOnlyCollection<RegisteredBerry> _getRegisteredBerries;
        private static ReadOnlyCollection<RegisteredBerry> _getTrackableBerries;
        private static ReadOnlyCollection<Type> _getBerryTypes;
        private static ReadOnlyCollection<string> _getBerryNames;
        private static HashSet<string> _trackedBerryNames;
        private static HashSet<string> _berryNamesHashSet;

        // Return caches or create new ones
        public static ReadOnlyCollection<RegisteredBerry> GetRegisteredBerries() {
            if (_getRegisteredBerries == null)
                _getRegisteredBerries = registeredBerries.ToList().AsReadOnly();
            return _getRegisteredBerries;
        }
        public static ReadOnlyCollection<RegisteredBerry> GetTrackableBerries() {
            if (_getTrackableBerries == null)
                _getTrackableBerries = registeredBerries.ToList().FindAll(berry => berry.isTracked).AsReadOnly();
            return _getTrackableBerries;
        }
        public static ReadOnlyCollection<Type> GetBerryTypes() {
            if (_getBerryTypes == null) {
                List<Type> types = new List<Type>();
                foreach (RegisteredBerry b in registeredBerries)
                    if (!types.Contains(b.berryClass)) // enumerate each type once. a berry can have multiple names.
                        types.Add(b.berryClass);

                _getBerryTypes = types.AsReadOnly();
            }
            return _getBerryTypes;
        }
        public static ReadOnlyCollection<string> GetBerryNames() {
            if (_getBerryNames == null) {
                List<string> berryNames = new List<string>(registeredBerries.Count);
                foreach (RegisteredBerry berry in registeredBerries) {
                    berryNames.Add(berry.entityName);
                }
                _getBerryNames = berryNames.AsReadOnly();
            }
            return _getBerryNames;
        }

        // Register the strawberry or similar collectible with the Strawberry Registry, allowing it to be auto-collected at level end and be trackable.
        public static void Register(Type type, string name, bool tracked = true, bool blocksNormalCollection = false) {
            registeredBerries.Add(new RegisteredBerry(type, name, tracked, blocksNormalCollection));

            // clear caches, so that next calls to the getter methods also include the berry that was just registered.
            _getRegisteredBerries = null;
            _getTrackableBerries = null;
            _getBerryTypes = null;
            _getBerryNames = null;
            _trackedBerryNames = null;
            _berryNamesHashSet = null;
        }

        // Register the strawberry or similar collectible with the Strawberry Registry, allowing it to be auto-collected at level end and be trackable.
        public static void Register(Type type, bool tracked = true, bool blocksNormalCollection = false) {
            if (Attribute.IsDefined(type, typeof(CustomEntityAttribute))) {
                CustomEntityAttribute attr = Attribute.GetCustomAttribute(type, typeof(CustomEntityAttribute)) as CustomEntityAttribute;
                foreach (string id in attr.IDs) {
                    Register(type, id, tracked, blocksNormalCollection);
                }
            }
        }

        public static bool TrackableContains(string name) {
            // create a HashSet for efficiently checking this, as this function gets called a lot.
            var berries = _trackedBerryNames ??= GetTrackableBerries()
                .Select(b => b.entityName)
                .ToHashSet();

            return berries.Contains(name);
        }

        public static bool TrackableContains(BinaryPacker.Element target) {
            if (target.AttrBool("moon", false))
                return false;

            return TrackableContains(target.Name);
        }

        /// <summary>
        /// Checks whether the given name represents a registered berry.
        /// </summary>
        public static bool IsRegisteredBerry(string name) {
            // create a HashSet for efficiently checking this, as this function gets called a lot.
            _berryNamesHashSet ??= GetBerryNames().ToHashSet();

            return _berryNamesHashSet.Contains(name);
        }

        // Is it the first normally collectable strawberry in the train?
        public static bool IsFirstStrawberry(Entity self) {
            Follower follow = self.Components.Get<Follower>();
            if (follow == null)
                return false;

            // we need it searchable and Find doesn't work on a ReadOnlyCollection aaaaa
            List<RegisteredBerry> berries = registeredBerries.ToList();
            ReadOnlyCollection<Type> types = GetBerryTypes();

            for (int i = follow.FollowIndex - 1; i >= 0; i--) {
                Entity strawberry = follow.Leader.Followers[i].Entity;
                Type t = strawberry.GetType();
                RegisteredBerry berry = berries.Find(b => b.berryClass == t);

                // Is the "strawberry" registered? If not, bail immediately.
                // This is a safety check and should _never_ fail.
                bool isRegistered = types.Contains(strawberry.GetType());

                // Does the strawberry not collect in the normal way?
                // If it does, we need to defer "leader" to another berry.
                bool blocksCollect = false;
                if (berry != null)
                    blocksCollect = berry.blocksNormalCollection;

                // Is it a vanilla Gold Strawberry?
                // This needs to defer "leader" to another berry.
                bool goldenCheck = false;
                if (strawberry as Strawberry != null)
                    goldenCheck = (strawberry as Strawberry).Golden;

                // Did we find a berry closer to the front that doesn't defer?
                // It must be registered, must NOT block collection, and is NOT a gold berry
                // in order to be a valid leader.
                if (types.Contains(strawberry.GetType()) &&
                    !blocksCollect &&
                    !goldenCheck) {
                    return false;
                }
            }
            return true;
        }

        public class RegisteredBerry {
            // The registered berry as a Type
            public readonly Type berryClass;

            // The custom name for the berry.
            public readonly string entityName;

            // T: add berry to tracker and auto-assign checkpoint + order
            // F: berry is secret, only auto-handle last ditch collections
            public readonly bool isTracked;

            // T: berry defers "leadership" of the berry train so that berries that follow redberry collection rules can be collected
            // F: berry doesn't have a special collection rule and can be a "leader" of the berry train
            public readonly bool blocksNormalCollection;

            public RegisteredBerry(Type berry, string name, bool track, bool blockCollect) {
                berryClass = berry;
                entityName = name;
                isTracked = track;
                blocksNormalCollection = blockCollect;
            }
        }
    }
}
