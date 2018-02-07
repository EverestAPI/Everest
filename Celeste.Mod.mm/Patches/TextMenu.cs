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

namespace Celeste {
    class patch_TextMenu : TextMenu {

        // We're effectively in TextMenu, but still need to "expose" private fields to our mod.
        private List<Item> items;
        public List<Item> Items => items;

        // Basically the same as Add(), but with an index parameter.
        public TextMenu Insert(int index, Item item) {
            items.Insert(index, item);
            item.Container = this;

            Add(item.ValueWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
            Add(item.SelectWiggler = Wiggler.Create(0.25f, 3f, null, false, false));

            item.ValueWiggler.UseRawDeltaTime = item.SelectWiggler.UseRawDeltaTime = true;

            if (Selection == -1)
                FirstSelection();

            RecalculateSize();
            item.Added();
            return this;
        }

        // The reverse of Add()
        public TextMenu Remove(Item item) {
            int index = items.IndexOf(item);
            if (index == -1)
                return this;
            items.RemoveAt(index);
            item.Container = null;

            Remove(item.ValueWiggler);
            Remove(item.SelectWiggler);

            RecalculateSize();
            return this;
        }

    }
    public static class TextMenuExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static List<TextMenu.Item> GetItems(this TextMenu self)
            => ((patch_TextMenu) self).Items;

        public static TextMenu Insert(this TextMenu self, int index, TextMenu.Item item)
            => ((patch_TextMenu) self).Insert(index, item);

        public static TextMenu Remove(this TextMenu self, TextMenu.Item item)
            => ((patch_TextMenu) self).Remove(item);

    }
}
