#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections.Generic;

namespace Celeste {
    class patch_Pathfinder : Pathfinder {
        // Those types are private in Pathfinder and needed to define the types of the private fields.
        private struct Tile { }
        private class PointMapComparer {
            public PointMapComparer(Tile[,] map) { }
        }

        // We're effectively in Pathfinder, but still need to "expose" private fields to our mod.
        private Level level;
        private Tile[,] map;
        private PointMapComparer comparer;

        public patch_Pathfinder(Level level)
            : base(level) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern bool orig_Find(ref List<Vector2> path, Vector2 from, Vector2 to, bool fewerTurns = true, bool logging = false);
        public new bool Find(ref List<Vector2> path, Vector2 from, Vector2 to, bool fewerTurns = true, bool logging = false) {
            int neededWidth = level.Bounds.Width / 8;
            int neededHeight = level.Bounds.Height / 8;

            // check if the "map" array is big enough for the pathfinding we are asked to do here.
            if (map.GetLength(0) < neededWidth || map.GetLength(1) < neededHeight) {
                // extend it as required.
                int newWidth = Math.Max(map.GetLength(0), neededWidth);
                int newHeight = Math.Max(map.GetLength(1), neededHeight);

                map = new Tile[newWidth, newHeight];
                comparer = new PointMapComparer(map);
            }

            // then, run the pathfinding as vanilla would.
            return orig_Find(ref path, from, to, fewerTurns, logging);
        }

        [MonoModIgnore] // we don't want to change anything in the method...
        [PatchPathfinderRender] // ... except manipulating it manually via MonoModRules
        public extern new void Render();
    }
}
