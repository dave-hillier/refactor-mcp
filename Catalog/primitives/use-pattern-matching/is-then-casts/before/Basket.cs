using System;

namespace Shop
{
    public class Product
    {
        public string Name = "";
        public decimal Price;
    }

    public class Basket
    {
        public decimal Total;

        public void Add(object item)
        {
            /*^*/if (item is Product)
            {
                Console.WriteLine(((Product)item).Name);
                Total += ((Product)item).Price;
            }
        }
    }
}
