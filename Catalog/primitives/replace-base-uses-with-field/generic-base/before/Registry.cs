using System.Collections.Generic;

namespace Shop
{
    public class Registry<T> : List<T>
    {
        private readonly List<T> _items = new List<T>();

        public void Register(T item)
        {
            if (!Contains(item))
                Add(item);
        }
    }
}
