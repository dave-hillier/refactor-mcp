using System.Collections.Generic;

namespace Shop
{
    public class Index<TKey>
    {
        private readonly Dictionary<TKey, Invoice> _entries = new Dictionary<TKey, Invoice>();

        public void Add(TKey key, Invoice value) => _entries[key] = value;

        public Index<TKey> With(TKey key, Invoice value)
        {
            Add(key, value);
            return this;
        }
    }
}
