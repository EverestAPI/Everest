using Celeste.Mod.Entities;
using Monocle;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Celeste.Mod
{
    /// <summary>
    /// Allows mods to register their own strawberry-type collectible objects.<para />
    /// Registered strawberries can be tracked or secret, and will attempt to autocollect when appropriate e.g. level end.
    /// </summary>
    public static class StrawberryRegistry
    {
        private static HashSet<RegisteredBerry> registeredBerries = new HashSet<RegisteredBerry>() { new RegisteredBerry(typeof(Strawberry), "strawberry", true, false) };

        // Caches
        private static ReadOnlyCollection<RegisteredBerry> _getRegisteredBerries;
        private static ReadOnlyCollection<RegisteredBerry> _getTrackableBerries;
        private static ReadOnlyCollection<Type> _getBerryTypes;

        public static ReadOnlyCollection<RegisteredBerry> GetRegisteredBerries()
        {
            if (_getRegisteredBerries == null)
                _getRegisteredBerries = registeredBerries.ToList().AsReadOnly();
            return _getRegisteredBerries;
        }
        public static ReadOnlyCollection<RegisteredBerry> GetTrackableBerries()
        {
            if (_getTrackableBerries == null)
                _getTrackableBerries = registeredBerries.ToList().FindAll(berry => berry.isTracked).AsReadOnly();
            return _getTrackableBerries;
        }
        public static ReadOnlyCollection<Type> GetBerryTypes()
        {
            if (_getBerryTypes == null)
            {
                List<Type> types = new List<Type>();
                foreach (RegisteredBerry b in registeredBerries)
                    types.Add(b.berryClass);
                _getBerryTypes = types.AsReadOnly();
            }
            return _getBerryTypes;
        }

        // Register the strawberry or similar collectible with the Strawberry Registry, allowing it to be auto-collected at level end and be trackable.<para />
        // Only use this version if your strawberry class does not use the CustomEntity(entityname) attribute.
        public static void Register(Type type, string name, bool tracked = true, bool alternateCollectionRules = false)
        {
            registeredBerries.Add(new RegisteredBerry(type, name, tracked, alternateCollectionRules));
        }

        // Register the strawberry or similar collectible with the Strawberry Registry, allowing it to be auto-collected at level end and be trackable.
        public static void Register(Type type, bool tracked = true, bool alternateCollectionRules = false)
        {
            if (Attribute.IsDefined(type, typeof(CustomEntityAttribute)))
            {
                CustomEntityAttribute attr = Attribute.GetCustomAttribute(type, typeof(CustomEntityAttribute)) as CustomEntityAttribute;
                foreach (string id in attr.IDs)
                {
                    registeredBerries.Add(new RegisteredBerry(type, id, tracked, alternateCollectionRules));
                }
            }
        }

        public static bool TrackableContains(string name)
        {
            ReadOnlyCollection<RegisteredBerry> berries = GetTrackableBerries();
            foreach (RegisteredBerry berry in berries)
            {
                if (berry.entityName == name)
                    return true;
            }
            return false;
        }

        // Is it the first normally collectable strawberry in the train?
        public static bool IsFirstStrawberry(Entity self)
        {
            Follower follow = self.Components.Get<Follower>();
            if (follow == null)
                return false;

            // we need it searchable and Find doesn't work on a ReadOnlyCollection aaaaa
            List<RegisteredBerry> berries = registeredBerries.ToList();
            ReadOnlyCollection<Type> types = GetBerryTypes();
            //RegisteredBerry detectedBerry = berries.Find(b => b.GetType() == self.GetType());
            //if (detectedBerry != null)
            //if (detectedBerry == null)
            //    return false;

            for (int i = follow.FollowIndex - 1; i >= 0; i--)
            {
                Entity strawberry = follow.Leader.Followers[i].Entity;
                bool goldenCheck = false;
                if (strawberry as Strawberry != null)
                    goldenCheck = (strawberry as Strawberry).Golden;

                //Strawberry vanillaStraw = strawberry as Strawberry;
                //if (vanillaStraw != null && !vanillaStraw.Golden)
                //{
                //    return false;
                //}
                Type t = follow.Entity.GetType();
                RegisteredBerry berry = berries.Find(b => b.berryClass == t);
                bool blocksCollect = berry.blocksNormalCollection;
                if (types.Contains(strawberry.GetType()) && 
                    berry.blocksNormalCollection == false && 
                    !goldenCheck)
                {
                    return false;
                }
            }
            return true;
        }

        // Called by CoreMapDataProcessor in order to inject mod berries into the step list for fixup.
        public static Dictionary<string, Action<BinaryPacker.Element>> GetBerriesToInject(MapDataFixup context)
        {
            Dictionary<string, Action<BinaryPacker.Element>> trackDict = new Dictionary<string, Action<BinaryPacker.Element>>();
            ReadOnlyCollection<RegisteredBerry> berries = GetRegisteredBerries();
            foreach (RegisteredBerry berry in berries)
            {
                trackDict.Add(String.Concat("entity:", berry.entityName), entity => { context.Run("entity:strawberry", entity); });
            }

            return trackDict;
        }

        public class RegisteredBerry
        {
            // registered berry as a Type
            public readonly Type berryClass;

            // entity:<passed berry's defined name>
            public readonly string entityName;

            // T: add berry to tracker and auto-assign checkpoint + order
            // F: berry is secret, only auto-handle last ditch collections
            public readonly bool isTracked;

            public readonly bool blocksNormalCollection;

            public RegisteredBerry(Type berry, string name, bool track, bool blockCollect)
            {
                berryClass = berry;
                entityName = name;
                isTracked = track;
                blocksNormalCollection = blockCollect;
            }
        }
    }
}
