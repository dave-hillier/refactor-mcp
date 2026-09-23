#nullable enable

using System.Collections.Generic;

namespace Shop
{
    public class Cache<T>
    {
        private readonly Dictionary<string, T> _items = new Dictionary<string, T>();

        public T? Get(string key)
        {
            return _items.TryGetValue(key, out var value) ? value : default;
        }
    }
}
