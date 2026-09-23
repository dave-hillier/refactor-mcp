using System;

namespace Shop
{
    public class Stock
    {
        public void Check(object item, int level)
        {
            // Only products are counted.
            /*^*/if (item is Product product)
            {
                // Low stock needs attention.
                if (level < product.Minimum)
                {
                    Console.WriteLine(product.Name); // reorder
                }
            }
        }
    }

    public class Product
    {
        public string Name = "";
        public int Minimum;
    }
}
