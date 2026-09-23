using System;

namespace Shop
{
    public class Shipping
    {
        public void Ship(string order)
        {
            /*^*/if (order != null)
            {
                Console.WriteLine(order);
            }

            Console.WriteLine("done");
        }
    }
}
