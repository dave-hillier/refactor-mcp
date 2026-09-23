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
            /*^*/if (order != null)
            {
                // Only paid orders are packed.
                if (order.IsPaid)
                {
                    Console.WriteLine("packing");
                    Console.WriteLine(order.Id);
                }
            }
        }
    }
}
