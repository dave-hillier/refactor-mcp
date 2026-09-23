using System.Collections.Generic;

namespace Shop
{
    public class Repository<T> : Store<T>
    {
        private readonly List<T> _items = new List<T>();

        public override int Count => _items.Count;
    }
}
