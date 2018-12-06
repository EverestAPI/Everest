#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;
using System.IO;
using FMOD.Studio;
using Monocle;
using Celeste.Mod.Meta;

namespace Celeste {
    class patch_SinkingPlatform : SinkingPlatform {

        public string OverrideTexture;

        public patch_SinkingPlatform(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            AreaData area = AreaData.Get(scene);

            string prevTexture = area.WoodPlatform;
            if (OverrideTexture != null)
                area.WoodPlatform = OverrideTexture;

            orig_Added(scene);

            area.WoodPlatform = prevTexture;
        }

    }
}
