using System.Collections.Generic;

namespace Shop;

public class Shelf<T>(List<T> items) where T : class
{
    public T First() => items.Count > 0 ? items[0] : null;
}
