using System.Collections.Generic;

namespace Shop;

public class Boxes
{
    public IReadOnlyList<T> Wrap<T>(T item)
    {
        return new List<T> { item };
    }

    public int Size()
    {
        return Wrap(5).Count + Wrap("five").Count;
    }
}
