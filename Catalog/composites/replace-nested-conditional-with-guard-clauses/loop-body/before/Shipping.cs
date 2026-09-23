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
                /*^*/if (item != null)
                {
                    if (stock > 0 && item.Length > 0)
                    {
                        Console.WriteLine(item);
                    }
                }
            }
        }
    }
}
