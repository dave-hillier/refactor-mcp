using System.Collections.Generic;

public class Cache<T>
{
    public int Store(T item)
    {
        var /*^*/items = new List<T>();
        items.Add(item);
        return items.Count;
    }
}
