using System;
using System.Collections.Generic;

namespace Shop
{
    public class Store<T>
    {
        private readonly Dictionary<string, T> _items = new Dictionary<string, T>();

        public event Action<T, string?>? Saved;

        public void Save(T item, string? key)
        {
            _items[key ?? "default"] = item;
            Saved?.Invoke(item, key);
        }
    }
}
