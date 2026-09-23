using System.Collections.Generic;

namespace Shop
{
    public class Index<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _entries = new Dictionary<TKey, TValue>();

        public void Add(TKey key, TValue value) => _entries[key] = value;

        public Index<TKey, TValue> With(TKey key, TValue value)
        {
            Add(key, value);
            return this;
        }
    }
}
