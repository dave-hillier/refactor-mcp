using System;

namespace Shop
{
    public class Order
    {
        public decimal Total { get; set; }

        public bool IsPaid { get; set; }
    }

    public class Dispatch
    {
        public void Send(Order order)
        {
            // Priority orders go first.
            if (IsPriority(order))
            {
                Console.WriteLine("priority");
            }
        }

        private bool IsPriority(Order order)
        {
            return order.Total > 100m // large orders
                && order.IsPaid;
        }
    }
}
