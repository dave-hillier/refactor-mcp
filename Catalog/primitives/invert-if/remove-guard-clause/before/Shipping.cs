using System;

namespace Shop
{
    public class Shipping
    {
        public void Ship(string order)
        {
            /*^*/if (order == null)
            {
                return;
            }

            // Only real orders are packed.
            Console.WriteLine("packing");
            Console.WriteLine(order);
        }
    }
}
