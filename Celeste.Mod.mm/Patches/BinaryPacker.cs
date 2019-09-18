#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    static class patch_BinaryPacker {

        [MonoModIgnore] // We don't want to change anything about the method...
        [ProxyFileCalls] // ... except for proxying all System.IO.File.* calls to Celeste.Mod.FileProxy.*
        public static extern BinaryPacker.Element FromBinary(string filename);

        public class Element : BinaryPacker.Element {
            public extern bool orig_HasAttr(string name);
            public new bool HasAttr(string name)
                => orig_HasAttr(name) || orig_HasAttr(name.ToLowerInvariant());

            public extern string orig_Attr(string name, string defaultValue = "");
            public new string Attr(string name, string defaultValue = "")
                => orig_Attr(name, orig_Attr(name.ToLowerInvariant(), defaultValue));

            public extern bool orig_AttrBool(string name, bool defaultValue = false);
            public new bool AttrBool(string name, bool defaultValue = false)
                => orig_AttrBool(name, orig_AttrBool(name.ToLowerInvariant(), defaultValue));

            public extern float orig_AttrFloat(string name, float defaultValue = 0f);
            public new float AttrFloat(string name, float defaultValue = 0f)
                => orig_AttrFloat(name, orig_AttrFloat(name.ToLowerInvariant(), defaultValue));
        }

    }
}
