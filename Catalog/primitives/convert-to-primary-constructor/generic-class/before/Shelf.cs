using System.Collections.Generic;

namespace Shop;

public class Shelf<T> where T : class
{
    private readonly List<T> _items;

    public Shelf(List<T> items)
    {
        _items = items;
    }

    public T First() => _items.Count > 0 ? _items[0] : null;
}
