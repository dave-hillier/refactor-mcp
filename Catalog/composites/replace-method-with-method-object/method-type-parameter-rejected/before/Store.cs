using System.Collections.Generic;

namespace Shop
{
    public class Store
    {
        public T First<T>(List<T> items)
        {
            T first = items[0];
            return first;
        }
    }
}
