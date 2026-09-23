using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
    }

    public class OrderStore : Store<Order>
    {
        protected List<Order> Cache = new List<Order>();

        public override int Count() => Cache.Count;
    }
}
