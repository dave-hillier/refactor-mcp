using System;

namespace Shop
{
    public class Shipping
    {
        public void Ship(string order)
        {
            /*^*/Console.WriteLine("checking");
            if (order != null)
            {
                Console.WriteLine(order);
            }
        }
    }
}
