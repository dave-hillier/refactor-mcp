using System;

namespace Shop
{
    public class Shipping
    {
        public void Ship(bool paid, int id)
        {
            if (!paid)
            {
                throw new InvalidOperationException("The order is not paid");
            }

            Console.WriteLine("packing");

            Console.WriteLine(id);
        }
    }
}
