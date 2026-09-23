using System;

namespace Shop
{
    public class Shipping
    {
        public void Announce(int zone)
        {
            switch (zone)
            {
                case 1:
                    Console.WriteLine("local");
                    break;
                case 2:
                case 3:
                    Console.WriteLine("national");
                    break;
                default:
                    Console.WriteLine("international");
                    break;
            }
        }
    }
}
