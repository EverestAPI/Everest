#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
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

        public extern void orig_Update();
        public override void Update() {
            orig_Update();

            if (Focused && Items.Any(item => item.Hoverable)) {
                if (CoreModule.Settings.MenuPageDown.Pressed && Selection != LastPossibleSelection) {
                    // move down
                    Current.OnLeave?.Invoke();
                    float startY = GetYOffsetOf(Current);
                    while (GetYOffsetOf(Current) < startY + 1080f && Selection < LastPossibleSelection) {
                        MoveSelection(1);
                    }
                    Audio.Play("event:/ui/main/rollover_down");
                    Current.OnEnter?.Invoke();
                } else if (CoreModule.Settings.MenuPageUp.Pressed && Selection != FirstPossibleSelection) {
                    // move up
                    Current.OnLeave?.Invoke();
                    float startY = GetYOffsetOf(Current);
                    while (GetYOffsetOf(Current) > startY - 1080f && Selection > FirstPossibleSelection) {
                        MoveSelection(-1);
                    }
                    Audio.Play("event:/ui/main/rollover_up");
                    Current.OnEnter?.Invoke();
                }
            }
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

        public class patch_Option<T> : Option<T> {
            private float cachedRightWidth;
            private List<string> cachedRightWidthContent;

            /// <summary>
            /// The color the text takes when the option is active, but unselected (defaults to white).
            /// </summary>
            public Color UnselectedColor;

            public patch_Option(string label) : base(label) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            [MonoModConstructor]
            public extern void orig_ctor(string label);

            [MonoModConstructor]
            public void ctor(string label) {
                cachedRightWidth = 0f;
                cachedRightWidthContent = new List<string>();
                UnselectedColor = Color.White;
                orig_ctor(label);
            }

            [MonoModIgnore]
            [PatchTextMenuOptionColor]
            public extern new void Render(Vector2 position, bool highlighted);

            public extern float orig_RightWidth();
            public override float RightWidth() {
                // the vanilla method measures each option, which can be resource-heavy.
                // caching it allows to remove some lag in big menus, like Mod Options with a lot of mods installed.
                List<string> currentContent = Values.Select(val => val.Item1).ToList();
                if (!cachedRightWidthContent.SequenceEqual(currentContent)) {
                    // contents changed, or the width wasn't computed yet.
                    cachedRightWidth = orig_RightWidth();
                    cachedRightWidthContent = currentContent;
                }
                return cachedRightWidth;
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
