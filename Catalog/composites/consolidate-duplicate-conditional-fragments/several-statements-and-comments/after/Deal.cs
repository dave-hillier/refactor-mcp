using System;

namespace Shop
{
    public class Deal
    {
        public decimal Price(decimal price, bool special)
        {
            decimal total;
            // Every deal is logged.
            Console.WriteLine("pricing");
            Console.WriteLine(price);

            if (special)
            {
                total = price * 0.95m;
            }
            else
            {
                total = price * 0.98m;
            }

            // Tell the customer.
            Console.WriteLine(total);
            return total;
        }
    }
}
