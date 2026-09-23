using System.Collections.Generic;

namespace Shop
{
    public class Repository<T> : Store<T>
    {
        public override int Count => _items.Count;
    }
}
