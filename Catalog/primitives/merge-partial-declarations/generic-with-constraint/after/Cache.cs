using System.Collections.Generic;

namespace Shop;

public class Cache<T> where T : class
{
    private readonly Dictionary<string, T> _items = new();

    public int Count => _items.Count;

    public void Store(string key, T value) => _items[key] = value;

    public T Find(string key) => _items.TryGetValue(key, out var value) ? value : null;
}
