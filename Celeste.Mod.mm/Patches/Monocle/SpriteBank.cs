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

namespace Monocle {
    class patch_SpriteBank : SpriteBank {

        public string XMLPath;

        public patch_SpriteBank(Atlas atlas, XmlDocument xml)
            : base(atlas, xml) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Patching constructors is ugly.
        public extern void orig_ctor_SpriteBank(Atlas atlas, XmlDocument xml);
        [MonoModConstructor]
        public void ctor_SpriteBank(Atlas atlas, XmlDocument xml) {
            orig_ctor_SpriteBank(atlas, xml);
            Everest.Content.Process(XMLPath, this);
        }

        public extern void orig_ctor_SpriteBank(Atlas atlas, string xmlPath);
        [MonoModConstructor]
        public void ctor_SpriteBank(Atlas atlas, string xmlPath) {
            XMLPath = xmlPath;
            orig_ctor_SpriteBank(atlas, xmlPath);
        }

    }
    public static class SpriteBankExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static string GetXMLPath(this SpriteBank self)
            => ((patch_SpriteBank) self).XMLPath;

    }
}
