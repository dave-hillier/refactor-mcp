using System.Collections.Generic;

namespace Shop
{
    public abstract class Store<TItem>
    {
        protected readonly List<TItem> _items = new List<TItem>();

        public abstract int Count { get; }
    }
}
