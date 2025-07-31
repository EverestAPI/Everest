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
        ///   when you want to enumerate a coroutine ienumerator,
        ///   always keep in mind that the original sequence may have been wrapped into <see cref="SwapImmediately"/>.
        ///   this method helps you to handle it correctly like <see cref="Coroutine"/>.<br/>
        ///   also known as, flattening the given sequence.
        /// </summary>
        /// <returns>
        ///   A new enumerator, but all <see cref="SwapImmediately"/> is safely handled.
        /// </returns>
        public static IEnumerator SafeEnumerate(this IEnumerator self) {
            Stack<IEnumerator> enums = new();
            enums.Push(self);

            while (enums.Count > 0) {
                IEnumerator cur = enums.Peek();

                if (cur.MoveNext()) {
                    object obj = cur.Current;

                    if (obj is SwapImmediately swap) {
                        enums.Push(swap.Inner);
                    } else {
                        yield return obj;
                    }
                } else {
                    enums.Pop();
                }
            }
        }
    }
}
