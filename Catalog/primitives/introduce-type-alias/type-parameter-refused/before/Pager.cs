using System.Collections.Generic;

namespace Shop
{
    public class Pager<T>
    {
        public /*^*/List<T> First(IEnumerable<T> items) => new List<T>(items);
    }
}
