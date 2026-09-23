using System;

namespace Shop
{
    public class Shipping
    {
        public void Announce(int zone)
        {
            /*^*/if (zone == 1)
            {
                Console.WriteLine("local");
            }
            else if (zone == 2 || zone == 3)
            {
                Console.WriteLine("national");
            }
            else
            {
                Console.WriteLine("international");
            }
        }
    }
}
