#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste {
    public class patch_TrailManager : TrailManager {
        public class patch_Snapshot : Snapshot {
            public extern void orig_Init(TrailManager manager, int index, Vector2 position, Image sprite, PlayerHair hair, Color color, float duration, int depth);

            public new void Init(TrailManager manager, int index, Vector2 position, Image sprite, PlayerHair hair, Color color, float duration, int depth) {
                orig_Init(manager, index, position, sprite, hair, color, duration, depth);

                // Fixed an issue with vanilla itself, player's body trail is always facing right 
                // It has been fixed since version 1.2.9.1
                // http://www.celestegame.com/temp/changelog_tech_test.txt
                // Fixed rendering bug where Madeline's dash trail could face the wrong way
                if (!Everest.Flags.IsDisabled && sprite != null && hair != null && Engine.Instance.Version < new Version(1, 2, 9, 1)) {
                    SpriteScale.X = SpriteScale.Abs().X * (int) hair.Facing;
                }
            }
        }
    }
}