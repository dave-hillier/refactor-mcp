using System;

namespace Shop
{
    public class Shipping
    {
        public decimal Rate(string zone)
        {
            /*^*/if (zone == "local")
            {
                Console.WriteLine("local rate");
                return 2.5m;
            }
            else
            {
                return 12m;
            }
        }
    }
}
