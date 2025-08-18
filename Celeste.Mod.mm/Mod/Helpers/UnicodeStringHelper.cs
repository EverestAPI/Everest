using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Core.Tokens;

namespace Celeste.Mod.Helpers {
    public static class UnicodeStringHelper {
        // helper class because manipulating generic types in IL is annoying
        public class ListInt {
            public required int[] Elements;
            public int Count => Elements.Length;
            public int this[int index] => Elements[index];
        }

        private class Cacher : CacheHelper<string, ListInt> {
            public Cacher() : base(name: "UnicodeStringHelper.ToCodePointList", maxSize: 1000) { }

            protected override ListInt Compute(string text) {
                return new ListInt {
                    Elements = text.EnumerateRunes().Select(r => r.Value).ToArray()
                };
            }
        }

        private static readonly Cacher cacher = new Cacher();

        public static ListInt ToCodePointList(string text) {
            return cacher.GetCached(text);
        }
    }
}
