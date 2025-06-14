using Celeste.Mod.Registry;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Monocle {
    class patch_Scene : Scene {

#pragma warning disable CS0414
        [MonoModIgnore]
    	public new event Action OnEndOfFrame;
#pragma warning restore CS0414

        [MonoModReplace]
        public new bool OnInterval(float interval) {
            return (int) (((double) TimeActive - Engine.DeltaTime) / interval) < (int) ((double) TimeActive / interval);
        }

        [MonoModReplace]
        public new bool OnInterval(float interval, float offset) {
            return Math.Floor(((double) TimeActive - offset - Engine.DeltaTime) / interval) < Math.Floor(((double) TimeActive - offset) / interval);
        }

                
        /// <summary>
        /// Finds all entities created from an EntityData with the specified SID, using the Tracker if possible.
        /// </summary>
        public IEnumerable<Entity> FindEntitiesWithSid(string sid) {
            var types = EntityRegistry.GetKnownTypesFromSid(sid);
            switch (types.Count)
            {
                case 0:
                    return Enumerable.Empty<Entity>();
                case 1:
                    return ((patch_Tracker)Tracker).GetEntitiesTrackIfNeeded(types.First());
                default:
                    return types.SelectMany(x => ((patch_Tracker)Tracker).GetEntitiesTrackIfNeeded(x));
            }
        }
        
        internal void ClearOnEndOfFrame() => OnEndOfFrame = null;

    }
}
