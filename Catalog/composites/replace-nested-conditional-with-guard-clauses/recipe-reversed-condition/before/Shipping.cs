using System;

namespace Shop
{
    public class Shipping
    {
        public void Ship(bool paid, int id)
        {
            /*^*/if (paid)
            {
                Console.WriteLine("packing");
            }
            else
            {
                throw new InvalidOperationException("The order is not paid");
            }

            Console.WriteLine(id);
        }
    }
}
