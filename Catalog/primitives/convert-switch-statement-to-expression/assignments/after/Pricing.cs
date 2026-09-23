using System;

namespace Shop
{
    public class Pricing
    {
        public void Print(string tier)
        {
            decimal rate;
            rate = tier switch
            {
                "gold" => 0.2m,
                "silver" => 0.1m,
                _ => 0m,
            };

            Console.WriteLine(rate);
        }
    }
}
