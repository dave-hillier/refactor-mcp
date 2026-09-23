using System;

namespace Shop
{
    public class Pricing
    {
        public void Print(string tier)
        {
            decimal rate;
            /*^*/switch (tier)
            {
                case "gold":
                    rate = 0.2m;
                    break;
                case "silver":
                    rate = 0.1m;
                    break;
                default:
                    rate = 0m;
                    break;
            }

            Console.WriteLine(rate);
        }
    }
}
