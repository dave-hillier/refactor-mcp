using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        public string Number { get; set; }

        public static void Register(Order order, List<Order> registry)
        {
            var Number = registry.Count.ToString();
            order.Number = Number;
            registry.Add(order);
        }
    }
}
