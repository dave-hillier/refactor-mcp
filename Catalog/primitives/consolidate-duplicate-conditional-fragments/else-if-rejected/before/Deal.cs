using System;

namespace Shop
{
    public class Deal
    {
        public void Price(decimal price, bool special, bool member)
        {
            if (special)
            {
                Console.WriteLine(price * 0.95m);
            }
            else /*^*/if (member)
            {
                Console.WriteLine(price * 0.98m);
                Console.WriteLine("sent");
            }
            else
            {
                Console.WriteLine(price);
                Console.WriteLine("sent");
            }
        }
    }
}
