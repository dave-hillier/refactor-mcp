using System;

namespace Shop
{
    public class Stock
    {
        public void Check(object item, int level)
        {
            // Only products are counted.
            if (item is Product product && level < product.Minimum)
            {
                // Low stock needs attention.
                Console.WriteLine(product.Name); // reorder
            }
        }
    }

    public class Product
    {
        public string Name = "";
        public int Minimum;
    }
}
