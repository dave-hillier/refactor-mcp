using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
    }

    public class Store<TItem>
    {
        protected readonly List<TItem> _items = new List<TItem>();
    }

    public class OrderStore : Store<Order>
    {
        public int Count() => _items.Count;
    }
}
