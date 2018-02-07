using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class Extensions {

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
        /// Invokes all delegates in the invocation list, as long as the last invoked .
        /// </summary>
        /// <typeparam name="T">Type of the result.</typeparam>
        /// <param name="md">The multicast delegate.</param>
        /// <param name="args">Any arguments that may be passed.</param>
        /// <returns>The result of the last invoked delegate.</returns>
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
        /// Split PascalCase words to become Pascal Case instead.
        /// </summary>
        /// <param name="input">PascalCaseString</param>
        /// <returns>Pascal Case String</returns>
        public static string SpacedPascalCase(this string input) {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < input.Length; i++) {
                char c = input[i];
                if (i > 0 && char.IsUpper(c))
                    builder.Append(' ');
                builder.Append(c);
            }
            return builder.ToString();
        }

        public static string DialogKeyify(this string input)
            => input.Replace('/', '_').Replace('-', '_');

        public static string DialogClean(this string input, Language language = null)
            => Dialog.Clean(input, language);

        public static string DialogCleanOrNull(this string input, Language language = null) {
            if (Dialog.Has(input))
                return Dialog.Clean(input);
            else
                return null;
        }

        public static Vector3? ToVector3(this float[] a) {
            if (a.Length != 3)
                return null;
            return new Vector3(a[0], a[1], a[2]);
        }

        public static TextMenu.Item NeedsRelaunch(this TextMenu.Item option, bool needsRelaunch) {
            if (!needsRelaunch)
                return option;
            return option
            .Enter(() => {
                // TODO: Show "needs relaunch" warning.
            })
            .Leave(() => {
                // TODO: Hide "needs relaunch" warning.
            });
        }

    }
}
