#pragma warning disable CS0414 // The field 'patch_IntroCrusher.triggered' is assigned but its value is never used
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections;
using System.Linq;

namespace Celeste {
    class patch_IntroCrusher : IntroCrusher {

        // We're effectively in IntroCrusher, but still need to "expose" private fields to our mod.
        private Vector2 end;
        private TileGrid tilegrid;

        public string levelFlags;

        [MonoModConstructor]
        [MonoModReplace]
        public patch_IntroCrusher(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, data.Height, data.Nodes[0] + offset) {

            levelFlags = data.Attr("flags");

            string tiletype = data.Attr("tiletype");
            if (!string.IsNullOrEmpty(tiletype)) {
                SurfaceSoundIndex = SurfaceIndex.TileToIndex[tiletype[0]];
                Remove(tilegrid);
                Add(tilegrid = GFX.FGAutotiler.GenerateBox(tiletype[0], data.Width / 8, data.Height / 8).TileGrid);
            }
        }

        [MonoModLinkTo("Monocle.Entity", "Added")]
        [MonoModIgnore]
        public extern void base_Added(Scene scene);
        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            Level level = scene as Level;
            if (level.Session.Area.GetLevelSet() == "Celeste" || string.IsNullOrEmpty(levelFlags)) {
                orig_Added(scene);
                return;
            }

            base_Added(scene);

            if (levelFlags.Split(',').Any(flag => level.Session.GetLevelFlag(flag))) {
                Position = end;
            } else {
                Add(new Coroutine(Sequence(), true));
            }
        }

        [MonoModIgnore]
        private extern IEnumerator Sequence();

    }
}
