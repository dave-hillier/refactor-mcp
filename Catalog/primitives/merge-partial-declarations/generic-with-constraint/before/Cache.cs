using System.Collections.Generic;

namespace Shop;

public partial class Cache<T>
{
    private readonly Dictionary<string, T> _items = new();

    public int Count => _items.Count;
}
