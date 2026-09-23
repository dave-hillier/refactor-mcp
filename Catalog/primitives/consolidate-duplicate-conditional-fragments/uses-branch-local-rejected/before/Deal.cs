using System;

namespace Shop
{
    public class Deal
    {
        public void Price(decimal price, bool special)
        {
            /*^*/if (special)
            {
                var total = price * 0.95m;
                Console.WriteLine(total);
            }
            else
            {
                var total = price * 0.98m;
                Console.WriteLine(total);
            }
        }
    }
}
