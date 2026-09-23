using System.Collections.Generic;

namespace Shop
{
    public class Cache<T>
    {
        private List<T> _snapshot;

        public int Count(IEnumerable<T> items)
        {
            _snapshot = new List<T>(items);
            return _snapshot.Count;
        }
    }
}
