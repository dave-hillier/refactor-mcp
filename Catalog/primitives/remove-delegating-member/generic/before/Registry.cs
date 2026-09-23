using System.Collections.Generic;

namespace Shop
{
    public class Registry<T> : List<T>
    {
        public new void Add(T item) => base.Add(item);

        public void Register(T item)
        {
            if (!Contains(item))
                Add(item);
        }
    }
}
