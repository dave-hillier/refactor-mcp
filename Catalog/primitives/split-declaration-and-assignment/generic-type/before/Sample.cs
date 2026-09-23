using System.Collections.Generic;

public class Sample
{
    public List<T> Twice<T>(T item)
    {
        var /*^*/items = new List<T>();
        items.Add(item);
        items.Add(item);
        return items;
    }
}
