using System;

namespace Shop
{
    public class Pricing
    {
        private decimal _rate;

        public decimal Rate => _rate;

        public void Apply(string tier)
        {
            switch (tier)
            {
                case "gold":
                    _rate = 0.2m;
                    break;
                case "silver":
                    _rate = 0.1m;
                    break;
                default:
                    throw new ArgumentException("unknown tier");
            }
        }
    }
}
