using System;

namespace Shop
{
    public class Shipping
    {
        public void Ship(string order, bool paid)
        {
            /*^*/if (order != null && paid)
            {
                Console.WriteLine(order);
            }
        }
    }
}
