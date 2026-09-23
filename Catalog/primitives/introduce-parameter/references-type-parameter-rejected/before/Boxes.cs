using System.Collections.Generic;

namespace Shop;

public class Boxes
{
    public int Count<T>(T item)
    {
        return /*[*/new List<T> { item }/*]*/.Count;
    }
}
