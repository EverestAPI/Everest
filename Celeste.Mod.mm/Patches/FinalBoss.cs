#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Entities;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_FinalBoss : FinalBoss {

        private bool canChangeMusic;

        public patch_FinalBoss(EntityData e, Vector2 offset)
            : base(e, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            canChangeMusic = data.Bool("canChangeMusic", true);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchBadelineBossOnPlayer] // ... except for manually manipulating the method via MonoModRules
        public new extern void OnPlayer(Player player);

        public bool CanChangeMusic(bool value) {
            Level level = Scene as Level;
            if (level.Session.Area.GetLevelSet() == "Celeste")
                return value;

            return canChangeMusic;
        }

    }
}
