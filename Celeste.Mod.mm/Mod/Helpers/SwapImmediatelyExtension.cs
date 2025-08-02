using Monocle;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Celeste.Mod {
    public static class SwapImmediatelyExtension {
        /// <summary>
        ///   Flattens all <see cref="SwapImmediately"/> returned by the source IEnumerator.
        ///   When writing hooks on Coroutine-style methods, 
        ///   this method is needed if you want to access values returned from the orig Coroutine,
        ///   for instance, if it were to return the following sequence `{1, 2, SwapImmediately({1, 2}), 3}`
        ///   a simple enumeration of it would not pass properly the values inside the <see cref="SwapImmediately" />.
        ///   This method makes it so the returned values become the sequence `{1, 2, 1, 2, 3}`.
        ///   (Behaviour mimicks <see cref="Coroutine"/>'s handling of <see cref="SwapImmediately"/>.)
        /// </summary>
        /// <returns>
        ///   A new enumerator which flattens the sequence once <see cref="SwapImmediately" />s are received.
        /// </returns>
        /// <example>
        /// The most common use would be hooking a Coroutine method:
        /// <code>
        ///   private static IEnumerator DashCoroutine(On.Celeste.Player.orig_DashCoroutine orig, Player self) {
        ///     IEnumerator origEnum = orig(self).SafeEnumerate();
        ///     while (origEnum.MoveNext()) {
        ///       yield return origEnum.Current;
        ///       // Do anything here
        ///     }
        ///   }
        /// </code>
        /// </example>
        public static Flattened SafeEnumerate(this IEnumerator self) {
            if(self is Flattened f) {
                return f;
            }
            return new(self);
        }

        public struct Flattened : IEnumerator {
            private readonly Stack<IEnumerator> enums = new();
            private object current = null;

            public readonly object Current => current;

            public Flattened(IEnumerator from) {
                enums.Push(from);
            }

            public bool MoveNext() {
                while (enums.Count > 0) {
                    IEnumerator cur = enums.Peek();

                    if (cur.MoveNext()) {
                        object obj = cur.Current;

                        if (obj is SwapImmediately swap) {
                            enums.Push(swap.Inner);
                        } else {
                            current = obj;
                            return true;
                        }
                    } else {
                        enums.Pop();
                    }
                }
                current = null;
                return false;
            }

            public void Reset() {
                throw new NotSupportedException();
            }
        }
    }
}
