using System.Collections.Generic;

public class Cache<T>
{
    private List<T> _items;

    public int Store(T item)
    {
        _items = new List<T>();
        _items.Add(item);
        return _items.Count;
    }
}
