using System;

namespace Shop
{
    public class Order
    {
        public int Id { get; set; }

        public bool IsPaid { get; set; }
    }

    public class Shipping
    {
        public void Ship(Order order)
        {
            if (order == null)
            {
                return;
            }

            // Only paid orders are packed.
            if (!order.IsPaid)
            {
                return;
            }

            Console.WriteLine("packing");
            Console.WriteLine(order.Id);
        }
    }
}
