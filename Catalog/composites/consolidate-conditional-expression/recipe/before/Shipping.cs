using System;

namespace Shop
{
    public class Order
    {
        public int Id { get; set; }

        public bool IsPaid { get; set; }

        public int Items { get; set; }
    }

    public class Shipping
    {
        public void Ship(Order order)
        {
            /*^*/if (order != null)
            {
                if (order.IsPaid)
                {
                    if (order.Items > 0)
                    {
                        Console.WriteLine(order.Id);
                    }
                }
            }
        }
    }
}
