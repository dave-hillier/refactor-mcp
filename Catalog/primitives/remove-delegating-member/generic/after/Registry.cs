using System.Collections.Generic;

namespace Shop
{
    public class Registry<T> : List<T>
    {
        public void Register(T item)
        {
            if (!Contains(item))
                Add(item);
        }
    }
}
