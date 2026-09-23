using System;

namespace Shop
{
    public class Deal
    {
        public decimal Price(decimal price, bool special)
        {
            decimal total;
            /*^*/if (special)
            {
                // Every deal is logged.
                Console.WriteLine("pricing");
                Console.WriteLine(price);
                total = price * 0.95m;
                // Tell the customer.
                Console.WriteLine(total);
                return total;
            }
            else
            {
                Console.WriteLine("pricing");
                Console.WriteLine(price);
                total = price * 0.98m;
                Console.WriteLine(total);
                return total;
            }
        }
    }
}
