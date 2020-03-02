#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;

namespace Monocle {
    class patch_VirtualMap<T> : VirtualMap<T> {

        public patch_VirtualMap(int columns, int rows, T emptyValue = default)
            : base(columns, rows, emptyValue) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Make VirtualMap's item getter and setter perform a safety check because out of bounds things are getting too common.

        public extern T orig_get_Item(int x, int y);
        public T get_Item(int x, int y) {
            if (x < 0 || y < 0 || Columns <= x || Rows <= y)
                return EmptyValue;

            return orig_get_Item(x, y);
        }

        public extern void orig_set_Item(int x, int y, T value);
        public void set_Item(int x, int y, T value) {
            if (x < 0 || y < 0 || Columns <= x || Rows <= y)
                return;

            orig_set_Item(x, y, value);
        }

    }
}
