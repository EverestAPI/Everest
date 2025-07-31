using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Celeste.Mod {
    public static class SwapImmediatelyExtension {
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
