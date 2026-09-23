using System;

namespace Shop
{
    public class Deal
    {
        public void Price(decimal price, bool special)
        {
            /*^*/if (special)
            {
                Console.WriteLine("special");
                Console.WriteLine(price * 0.95m);
            }
            else
            {
                Console.WriteLine(price * 0.98m);
                Console.WriteLine("standard");
            }
        }
    }
}
