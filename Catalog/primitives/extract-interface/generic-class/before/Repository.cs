using System.Collections.Generic;

namespace Shop;

public class Repository<T> where T : class
{
    private readonly List<T> _items = new List<T>();

    public void Add(T item) => _items.Add(item);

    public IReadOnlyList<T> All() => _items;
}
