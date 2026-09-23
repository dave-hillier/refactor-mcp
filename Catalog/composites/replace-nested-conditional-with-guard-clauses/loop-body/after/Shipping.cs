using System;
using System.Collections.Generic;

namespace Shop
{
    public class Shipping
    {
        public void ShipAll(List<string> items, int stock)
        {
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                if (stock <= 0 || item.Length <= 0)
                {
                    continue;
                }

                Console.WriteLine(item);
            }
        }
    }
}
