using System.Collections.Generic;

namespace Shop
{
    public class Cache<T>
    {
        public int Count(IEnumerable<T> items)
        {
            return /*[*/new List<T>(items)/*]*/.Count;
        }
    }
}
