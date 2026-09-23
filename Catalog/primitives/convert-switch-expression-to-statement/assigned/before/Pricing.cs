using System;

namespace Shop
{
    public class Pricing
    {
        private decimal _rate;

        public decimal Rate => _rate;

        public void Apply(string tier)
        {
            _rate = tier /*^*/switch
            {
                "gold" => 0.2m,
                "silver" => 0.1m,
                _ => throw new ArgumentException("unknown tier"),
            };
        }
    }
}
