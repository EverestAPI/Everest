using Ionic.Zip;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Monocle;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Celeste.Mod {
    public static class Extensions {

        /// <summary>
        /// Create a new MemoryStream for a given ZipEntry, which is safe to use in outside contexts.
        /// </summary>
        /// <param name="entry">The input ZipEntry.</param>
        /// <returns>The MemoryStream holding the extracted data of the ZipEntry.</returns>
        public static MemoryStream ExtractStream(this ZipEntry entry) {
            MemoryStream ms = new MemoryStream();
            entry.Extract(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        /// <summary>
        /// Create a hexadecimal string for the given bytes.
        /// </summary>
        /// <param name="data">The input bytes.</param>
        /// <returns>The output hexadecimal string.</returns>
        public static string ToHexadecimalString(this byte[] data)
            => BitConverter.ToString(data).Replace("-", string.Empty);

        /// <summary>
        /// Invokes all delegates in the invocation list, passing on the result to the next.
        /// </summary>
        /// <typeparam name="T">Type of the result.</typeparam>
        /// <param name="md">The multicast delegate.</param>
        /// <param name="val">The initial value and first parameter.</param>
        /// <param name="args">Any other arguments that may be passed.</param>
        /// <returns>The result of all delegates.</returns>
        public static T InvokePassing<T>(this MulticastDelegate md, T val, params object[] args) {
            if (md == null)
                return val;

            object[] args_ = new object[args.Length + 1];
            args_[0] = val;
            Array.Copy(args, 0, args_, 1, args.Length);

            Delegate[] ds = md.GetInvocationList();
            for (int i = 0; i < ds.Length; i++)
                args_[0] = ds[i].DynamicInvoke(args_);

            return (T) args_[0];
        }

        /// <summary>
        /// Invokes all delegates in the invocation list, as long as the previously invoked delegate returns true.
        /// </summary>
        public static bool InvokeWhileTrue(this MulticastDelegate md, params object[] args) {
            if (md == null)
                return true;

            Delegate[] ds = md.GetInvocationList();
            for (int i = 0; i < ds.Length; i++)
                if (!((bool) ds[i].DynamicInvoke(args)))
                    return false;

            return true;
        }

        /// <summary>
        /// Invokes all delegates in the invocation list, as long as the previously invoked delegate returns false.
        /// </summary>
        public static bool InvokeWhileFalse(this MulticastDelegate md, params object[] args) {
            if (md == null)
                return false;

            Delegate[] ds = md.GetInvocationList();
            for (int i = 0; i < ds.Length; i++)
                if ((bool) ds[i].DynamicInvoke(args))
                    return true;

            return false;
        }

        /// <summary>
        /// Invokes all delegates in the invocation list, as long as the previously invoked delegate returns null.
        /// </summary>
        public static T InvokeWhileNull<T>(this MulticastDelegate md, params object[] args) where T : class {
            if (md == null)
                return null;

            Delegate[] ds = md.GetInvocationList();
            for (int i = 0; i < ds.Length; i++) {
                T result = (T) ds[i].DynamicInvoke(args);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Split PascalCase words to become Pascal Case instead.
        /// </summary>
        /// <param name="input">PascalCaseString</param>
        /// <returns>Pascal Case String</returns>
        public static string SpacedPascalCase(this string input) {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < input.Length; i++) {
                char c = input[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(input[i - 1]))
                    builder.Append(' ');
                builder.Append(c);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Escape some common strings from a given string for usage with the Dialog class.
        /// The following characters get replaced with an underscore: /-+
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The Dialog-compatible key.</returns>
        public static string DialogKeyify(this string input)
            => input.Replace('/', '_').Replace('-', '_').Replace('+', '_').Replace(' ', '_');

        /// <summary>
        /// Get the dialog string for the given input key.
        /// </summary>
        /// <param name="input">The dialog key.</param>
        /// <param name="language"></param>
        /// <returns>The resolved dialog string.</returns>
        public static string DialogClean(this string input, Language language = null)
            => Dialog.Clean(input, language);

        /// <summary>
        /// Get the dialog string for the given input key.
        /// </summary>
        /// <param name="input">The dialog key.</param>
        /// <param name="language"></param>
        /// <returns>The resolved dialog string or null.</returns>
        public static string DialogCleanOrNull(this string input, Language language = null) {
            if (Dialog.Has(input, language))
                return Dialog.Clean(input, language);
            else
                return null;
        }

        /// <summary>
        /// Get a Vector2 from a Point.
        /// </summary>
        /// <param name="p">The input Point.</param>
        /// <returns>The output Vector2.</returns>
        public static Vector2 ToVector2(this Point p) {
            return new Vector2(p.X, p.Y);
        }

        /// <summary>
        /// Get a Vector2 from any float[] with a length of 2.
        /// </summary>
        /// <param name="a">The input array.</param>
        /// <returns>The output Vector2 or null if the length doesn't match.</returns>
        public static Vector2? ToVector2(this float[] a) {
            if (a == null)
                return null;
            if (a.Length == 1)
                return new Vector2(a[0]);
            if (a.Length != 2)
                return null;
            return new Vector2(a[0], a[1]);
        }

        /// <summary>
        /// Get a Vector3 from any float[] with a length of 3.
        /// </summary>
        /// <param name="a">The input array.</param>
        /// <returns>The output Vector3 or null if the length doesn't match.</returns>
        public static Vector3? ToVector3(this float[] a) {
            if (a == null)
                return null;
            if (a.Length == 1)
                return new Vector3(a[0]);
            if (a.Length != 3)
                return null;
            return new Vector3(a[0], a[1], a[2]);
        }

        /// <summary>
        /// Add an Enter and Leave handler, notifying the user that a relaunch is required to apply the changes.
        /// </summary>
        /// <param name="option">The input TextMenu.Item option.</param>
        /// <param name="containingMenu">The menu containing the TextMenu.Item option.</param>
        /// <param name="needsRelaunch">This method does nothing if this is set to false.</param>
        /// <returns>The passed option.</returns>
        public static TextMenu.Item NeedsRelaunch(this TextMenu.Item option, TextMenu containingMenu, bool needsRelaunch = true) {
            if (!needsRelaunch)
                return option;

            // build the "Restart is required" text menu entry
            TextMenuExt.EaseInSubHeaderExt needsRelaunchText = new TextMenuExt.EaseInSubHeaderExt(Dialog.Clean("MODOPTIONS_NEEDSRELAUNCH"), false, containingMenu) {
                TextColor = Color.OrangeRed,
                HeightExtra = 0f
            };

            List<TextMenu.Item> items = containingMenu.GetItems();
            if (items.Contains(option)) {
                // insert the text after the option that needs relaunch.
                containingMenu.Insert(items.IndexOf(option) + 1, needsRelaunchText);
            }

            option.OnEnter += delegate {
                // make the description appear.
                needsRelaunchText.FadeVisible = true;
            };
            option.OnLeave += delegate {
                // make the description disappear.
                needsRelaunchText.FadeVisible = false;
            };

            return option;
        }

        /// <summary>
        /// Add an Enter and Leave handler, displaying a description if selected.
        /// </summary>
        /// <param name="option">The input TextMenu.Item option.</param>
        /// <param name="containingMenu">The menu containing the TextMenu.Item option.</param>
        /// <param name="description"></param>
        /// <returns>The passed option.</returns>
        public static TextMenu.Item AddDescription(this TextMenu.Item option, TextMenu containingMenu, string description) {
            // build the description menu entry
            TextMenuExt.EaseInSubHeaderExt descriptionText = new TextMenuExt.EaseInSubHeaderExt(description, false, containingMenu) {
                TextColor = Color.Gray,
                HeightExtra = 0f
            };

            List<TextMenu.Item> items = containingMenu.GetItems();
            if (items.Contains(option)) {
                // insert the description after the option.
                containingMenu.Insert(items.IndexOf(option) + 1, descriptionText);
            }

            option.OnEnter += delegate {
                // make the description appear.
                descriptionText.FadeVisible = true;
            };
            option.OnLeave += delegate {
                // make the description disappear.
                descriptionText.FadeVisible = false;
            };

            return option;
        }

        // Celeste already ships with this.
        /*
        public static string ReadNullTerminatedString(this BinaryReader stream) {
            string text = "";
            char c;
            while ((c = stream.ReadChar()) > '\0') {
                text += c.ToString();
            }
            return text;
        }
        */

        /// <summary>
        /// Write the string to the BinaryWriter in a C-friendly format.
        /// </summary>
        /// <param name="stream">The output which the method writes to.</param>
        /// <param name="text">The input string.</param>
        public static void WriteNullTerminatedString(this BinaryWriter stream, string text) {
            if (text != null) {
                for (int i = 0; i < text.Length; i++) {
                    char c = text[i];
                    stream.Write(c);
                }
            }
            stream.Write('\0');
        }

        /// <summary>
        /// Cast a delegate from one type to another. Compatible with delegates holding an invocation list (combined delegates).
        /// </summary>
        /// <param name="source">The input delegate.</param>
        /// <param name="type">The wanted output delegate type.</param>
        /// <returns>The output delegate.</returns>
        public static Delegate CastDelegate(this Delegate source, Type type) {
            if (source == null)
                return null;
            Delegate[] delegates = source.GetInvocationList();
            if (delegates.Length == 1)
                return Delegate.CreateDelegate(type, delegates[0].Target, delegates[0].Method);
            Delegate[] delegatesDest = new Delegate[delegates.Length];
            for (int i = 0; i < delegates.Length; i++)
                delegatesDest[i] = delegates[i].CastDelegate(type);
            return Delegate.Combine(delegatesDest);
        }

        /// <summary>
        /// Map the list of buttons to the given virtual button.
        /// </summary>
        /// <param name="vbtn">The virtual button to map the buttons to.</param>
        /// <param name="buttons">The buttons to map.</param>
        public static void AddButtons(this patch_VirtualButton vbtn, List<Buttons> buttons) {
            foreach (Buttons btn in buttons) {
                if (btn == Buttons.LeftTrigger) {
                    vbtn.Nodes.Add(new patch_VirtualButton.PadLeftTrigger(Input.Gamepad, 0.25f));
                    continue;
                }

                if (btn == Buttons.RightTrigger) {
                    vbtn.Nodes.Add(new patch_VirtualButton.PadRightTrigger(Input.Gamepad, 0.25f));
                    continue;
                }

                vbtn.Nodes.Add(new patch_VirtualButton.PadButton(Input.Gamepad, btn));
            }
        }

        /// <summary>
        /// Is the given touch state "down" (pressed or moved)?
        /// </summary>
        public static bool IsDown(this TouchLocationState state)
            => state == TouchLocationState.Pressed || state == TouchLocationState.Moved;

        /// <summary>
        /// Is the given touch state "up" (released or invalid)?
        /// </summary>
        public static bool IsUp(this TouchLocationState state)
            => state == TouchLocationState.Released || state == TouchLocationState.Invalid;

        [ThreadStatic]
        private static HashSet<string> _SafeTypes;
        public static bool IsSafe(this Type type) {
            _SafeTypes ??= new HashSet<string>();

            try {
                if (_SafeTypes.Contains(type.AssemblyQualifiedName))
                    return true;

                // "Probe" the type
                _ = type.Name;
                _ = type.Assembly.FullName;
                _ = type.Module.FullyQualifiedName;

                // Check declaring and base type
                if (!type.DeclaringType?.IsSafe() ?? false)
                    return false;
                if (!type.BaseType?.IsSafe() ?? false)
                    return false;

                _SafeTypes.Add(type.AssemblyQualifiedName);
                return true;
            } catch {
                return false;
            }
        }

        public static Type[] GetTypesSafe(this Assembly asm) {
            try {
                return asm.GetTypes().Where(t => t.IsSafe()).ToArray();
            } catch (ReflectionTypeLoadException e) {
                return e.Types.Where(t => t != null && t.IsSafe()).ToArray();
            }
        }

        public static BinaryPacker.Element SetAttr(this BinaryPacker.Element el, string name, object value) {
            if (el.Attributes == null)
                el.Attributes = new Dictionary<string, object>();
            el.Attributes[name] = value;
            return el;
        }

        public static int AttrInt(this BinaryPacker.Element el, string name, int defaultValue = 0) {
            if (el.Attributes == null || !el.Attributes.TryGetValue(name, out object obj))
                return defaultValue;
            if (obj is int)
                return (int) obj;
            return int.Parse(obj.ToString(), CultureInfo.InvariantCulture);
        }

        public static bool AttrRef(this BinaryPacker.Element el, string name, ref string value) {
            if (el.HasAttr(name)) {
                value = el.Attr(name);
                return true;
            }
            return false;
        }
        public static bool AttrRef(this BinaryPacker.Element el, string name, ref bool value) {
            if (el.HasAttr(name)) {
                value = el.AttrBool(name);
                return true;
            }
            return false;
        }
        public static bool AttrRef(this BinaryPacker.Element el, string name, ref float value) {
            if (el.HasAttr(name)) {
                value = el.AttrFloat(name);
                return true;
            }
            return false;
        }
        public static bool AttrRef(this BinaryPacker.Element el, string name, ref int value) {
            if (el.HasAttr(name)) {
                value = int.Parse(el.Attr(name));
                return true;
            }
            return false;
        }

        public static bool AttrIf(this BinaryPacker.Element el, string name, Action<string> value) {
            if (el.HasAttr(name)) {
                value(el.Attr(name));
                return true;
            }
            return false;
        }
        public static bool AttrIfBool(this BinaryPacker.Element el, string name, Action<bool> value) {
            if (el.HasAttr(name)) {
                value(el.AttrBool(name));
                return true;
            }
            return false;
        }
        public static bool AttrIfFloat(this BinaryPacker.Element el, string name, Action<float> value) {
            if (el.HasAttr(name)) {
                value(el.AttrFloat(name));
                return true;
            }
            return false;
        }
        public static bool AttrIfInt(this BinaryPacker.Element el, string name, Action<int> value) {
            if (el.HasAttr(name)) {
                value(int.Parse(el.Attr(name)));
                return true;
            }
            return false;
        }

    }
}
