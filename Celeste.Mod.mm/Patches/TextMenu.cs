#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    class patch_TextMenu : TextMenu {

        // We're effectively in TextMenu, but still need to "expose" private fields to our mod.
        private List<Item> items;

        /// <summary>
        /// The items contained in this menu.
        /// </summary>
        public List<Item> Items => items;

        // Basically the same as Add(), but with an index parameter.
        /// <summary>
        /// Insert a <see cref="TextMenu.Item"/> at position <paramref name="index"/> in the menu.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Remove a <see cref="TextMenu.Item"/> from the menu.
        /// </summary>
        /// <param name="item">A <see cref="TextMenu.Item"/> contained in this menu.</param>
        /// <returns></returns>
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

        /// <inheritdoc cref="TextMenu.GetYOffsetOf(Item)"/>
        [MonoModReplace]
        public new float GetYOffsetOf(Item targetItem) {
            // this is a small fix of the vanilla method to better support invisible menu items.
            if (targetItem == null)
                return 0f;

            float num = 0f;
            foreach (Item listItem in items) {
                if (listItem.Visible) // this is targetItem.Visible in vanilla.
                    num += listItem.Height() + ItemSpacing;

                if (listItem == targetItem)
                    break;
            }

            return num - targetItem.Height() * 0.5f - ItemSpacing;
        }

        [MonoModReplace]
        public override void Render() {
            // this is heavily based on the vanilla method, adding a check to skip rendering off-screen options.
            RecalculateSize();
            Vector2 currentPosition = Position - Justify * new Vector2(Width, Height);
            foreach (Item item in items) {
                if (item.Visible) {
                    float itemHeight = item.Height();
                    Vector2 drawPosition = currentPosition + new Vector2(0f, itemHeight * 0.5f + item.SelectWiggler.Value * 8f);
                    // skip rendering the option if it is off-screen.
                    if (drawPosition.Y + itemHeight * 0.5f > 0 && drawPosition.Y - itemHeight * 0.5f < Engine.Height) {
                        item.Render(drawPosition, Focused && Current == item);
                    }
                    currentPosition.Y += itemHeight + ItemSpacing;
                }
            }
        }

        public class patch_LanguageButton : LanguageButton {
            public patch_LanguageButton(string label, Language language)
                : base(label, language) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            [MonoModReplace]
            public override void Render(Vector2 position, bool highlighted) {
                float alpha = Container.Alpha;
                Color strokeColor = Color.Black * (alpha * alpha * alpha);

                ActiveFont.DrawOutline(
                    Label, position, new Vector2(0f, 0.5f), Vector2.One,
                    Disabled ? Color.DarkSlateGray : ((highlighted ? Container.HighlightColor : Color.White) * alpha),
                    2f, strokeColor
                );

                position += new Vector2(Container.Width - RightWidth(), 0f);

                for (int x = -1; x <= 1; x++) {
                    for (int y = -1; y <= 1; y++) {
                        if (x != 0 || y != 0) {
                            Language.Icon.DrawJustified(
                                position + new Vector2(x * 2f, y * 2f), new Vector2(0f, 0.5f),
                                strokeColor, 1f
                            );
                        }
                    }
                }

                Language.Icon.DrawJustified(
                    position, new Vector2(0f, 0.5f),
                    Color.White * alpha, 1f
                );
            }
        }
    }

    public static partial class TextMenuExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get a list of all items which have been added to the menu.
        /// </summary>
        public static List<TextMenu.Item> GetItems(this TextMenu self)
            => ((patch_TextMenu) self).Items;

        /// <summary>
        /// Insert the given menu item at the given index.
        /// </summary>
        public static TextMenu Insert(this TextMenu self, int index, TextMenu.Item item)
            => ((patch_TextMenu) self).Insert(index, item);

        /// <summary>
        /// Remove the given menu item from the menu.
        /// </summary>
        public static TextMenu Remove(this TextMenu self, TextMenu.Item item)
            => ((patch_TextMenu) self).Remove(item);

    }
}
