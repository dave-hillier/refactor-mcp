using System;

namespace Shop
{
    public class Order
    {
        public void Ship(object item, int quantity, bool paid)
        {
            if (item is string name)
            {
                if (quantity > 0 && paid)
                {
                    Console.WriteLine(name);
                }
            }
        }
    }
}
