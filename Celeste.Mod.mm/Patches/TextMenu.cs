#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste {
    public class patch_TextMenu : TextMenu {

        // We're effectively in TextMenu, but still need to "expose" private fields to our mod.
        private List<Item> items;

        private bool batchMode;

        /// <summary>
        /// The items contained in this menu.
        /// </summary>
        public List<Item> Items => items;


        /// <summary>
        /// When a menu is in batch mode, adding / removing items will not recalculate its size to improve performance.
        /// Size is recalculated immediately after batch mode is disabled.
        /// </summary>
        public bool BatchMode {
            get => batchMode;
            set {
                batchMode = value;
                if (!batchMode) {
                    RecalculateSize();
                }
            }
        }

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

            if (!BatchMode) {
                RecalculateSize();
            }
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

            if (!BatchMode) {
                RecalculateSize();
            }
            return this;
        }

        /// <inheritdoc cref="TextMenu.GetYOffsetOf(TextMenu.Item)"/>
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
            if (targetItem is TextMenuExt.OptionSubMenu optionSubMenuItem && !optionSubMenuItem.Focused) {
                return num - targetItem.Height() - ItemSpacing + optionSubMenuItem.TitleHeight * 0.5f;
            }
            return num - targetItem.Height() * 0.5f - ItemSpacing;
        }

#pragma warning disable CS0626 // extern method with no attribute
        public extern void orig_Update();
#pragma warning restore CS0626 // extern method with no attribute

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

            // render non-AboveAll items, then AboveAll items.
            if (renderItems(aboveAll: false)) {
                renderItems(aboveAll: true);
            }
        }

        // Renders AboveAll or non-AboveAll items depending on the passed parameter, and returns true if items were skipped.
        private bool renderItems(bool aboveAll) {
            bool skippedItems = false;

            Vector2 currentPosition = Position - Justify * new Vector2(Width, Height);
            foreach (Item item in items) {
                if (item.Visible) {
                    float itemHeight = item.Height();
                    if (aboveAll == item.AboveAll) {
                        Vector2 drawPosition = currentPosition + new Vector2(0f, itemHeight * 0.5f + item.SelectWiggler.Value * 8f);
                        // skip rendering the option if it is off-screen.
                        if (((patch_Item) item).AlwaysRender || (drawPosition.Y + itemHeight * 0.5f > 0 && drawPosition.Y - itemHeight * 0.5f < Engine.Height)) {
                            item.Render(drawPosition, Focused && Current == item);
                        }
                    } else {
                        skippedItems = true;
                    }
                    currentPosition.Y += itemHeight + ItemSpacing;
                }
            }

            return skippedItems;
        }

        /// <inheritdoc cref="TextMenu.Add(TextMenu.Item)"/>
        [MonoModReplace]
        public new TextMenu Add(Item item)
        {
            items.Add(item);
            item.Container = this;
            Add(item.ValueWiggler = Wiggler.Create(0.25f, 3f));
            Add(item.SelectWiggler = Wiggler.Create(0.25f, 3f));
            item.ValueWiggler.UseRawDeltaTime = item.SelectWiggler.UseRawDeltaTime = true;
            if (Selection == -1) {
                FirstSelection();
            }
            if (!BatchMode) {
                RecalculateSize();
            }
            item.Added();
            return this;
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

#pragma warning disable CS0626 // extern method with no attribute
            public extern float orig_RightWidth();
#pragma warning restore CS0626 // extern method with no attribute

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

        public class patch_Item : Item {
            /// <summary>
            /// Set this property to true to force the Item to render even when off-screen.
            /// </summary>
            public virtual bool AlwaysRender { get; } = false;

            /// <summary>
            /// Items that have AboveAll set to true will render above those that have it set to false.
            /// </summary>
            [MonoModIfFlag("V1:Input")]
            public new bool AboveAll = false;
        }

        public class patch_SubHeader : SubHeader {

            public patch_SubHeader(string title)
                : base(title, true) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            [MonoModLinkTo("Celeste.TextMenu/SubHeader", "System.Void .ctor(System.String,System.Boolean)")]
            [MonoModIgnore]
            public extern void ctor(string label, bool topPadding = true);

            [MonoModIfFlag("V2:SubHeader")]
            [MonoModConstructor]
            public void ctor(string label) {
                ctor(label, true);
            }

        }

        public class patch_Setting : Setting {

            public patch_Setting(string label, string value = "")
                : base(label, value) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            public patch_Setting(string label, Keys key)
                : this(label) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            public patch_Setting(string label, List<Keys> keys)
                : this(label) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            public patch_Setting(string label, Buttons btn)
                : this(label) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            public patch_Setting(string label, List<Buttons> buttons)
                : this(label) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            [MonoModLinkTo("Celeste.TextMenu/Setting", "System.Void .ctor(System.String,System.String)")]
            [MonoModIgnore]
            public extern void ctor(string label, string value = "");

            [MonoModConstructor]
            public void ctor(string label, Keys key) {
                ctor(label);
                Set(new List<Keys>() { key });
            }

            [MonoModConstructor]
            public void ctor(string label, List<Keys> keys) {
                ctor(label);
                Set(keys);
            }

            [MonoModConstructor]
            public void ctor(string label, Buttons btn) {
                ctor(label);
                Set(new List<Buttons>() { btn });
            }

            [MonoModConstructor]
            public void ctor(string label, List<Buttons> buttons) {
                ctor(label);
                Set(buttons);
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

        /// <inheritdoc cref="patch_TextMenu.Insert(int, TextMenu.Item)"/>
        public static TextMenu Insert(this TextMenu self, int index, TextMenu.Item item)
            => ((patch_TextMenu) self).Insert(index, item);

        /// <inheritdoc cref="patch_TextMenu.Remove(TextMenu.Item)"/>
        public static TextMenu Remove(this TextMenu self, TextMenu.Item item)
            => ((patch_TextMenu) self).Remove(item);

    }
}
