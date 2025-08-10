using System.Collections.Generic;

namespace Celeste.Mod.Helpers {
    /**
     * A helper class that wraps a <code>compute</code> function, in order to keep the
     * latest computed values in memory (holding up to <code>maxSize</code> values),
     * to help with performance or with repeated memory allocations.
     */
    public abstract class CacheHelper<Key, Value> {
        private readonly struct ValueNode {
            public LinkedListNode<Key> Node { get; init; }
            public Value Value { get; init; }
        }

        private readonly string name;
        private readonly int maxSize;
        private readonly Dictionary<Key, ValueNode> cache = new Dictionary<Key, ValueNode>();
        private readonly LinkedList<Key> cacheOldestToNewest = new LinkedList<Key>();

        public CacheHelper(string name, int maxSize) {
            this.name = name;
            this.maxSize = maxSize;
        }

        public Value GetCached(Key key) {
            lock (cache) {
                if (cache.TryGetValue(key, out ValueNode valueNode)) {
                    cacheOldestToNewest.Remove(valueNode.Node);
                    cacheOldestToNewest.AddLast(valueNode.Node);
                    return valueNode.Value;
                }

                Value value = Compute(key);
                while (cache.Count >= maxSize) {
                    Key keyToEvict = cacheOldestToNewest.First.Value;
                    cache.Remove(keyToEvict);
                    cacheOldestToNewest.RemoveFirst();
                }

                LinkedListNode<Key> node = cacheOldestToNewest.AddLast(key);
                cache.Add(key, new ValueNode { Node = node, Value = value });
                Logger.Verbose("CacheHelper", $"[{name}] Cached value for \"{key}\" => \"{value}\", cache size: {cache.Count}");
                return value;
            }
        }

        protected abstract Value Compute(Key key);

        public void Clear() {
            lock (cache) {
                cache.Clear();
                cacheOldestToNewest.Clear();
            }
        }
    }
}
