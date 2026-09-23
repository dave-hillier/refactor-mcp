using System;

namespace Shop
{
    public class Converter
    {
        // The last rate used.
        private decimal _rate = 1.1m;
        private decimal _converted;

        public decimal Convert(decimal amount)
        {
            var rate = _rate;

            // Round to whole cents.
            _converted = amount * rate;
            return Math.Round(_converted, 2); // banker's rounding
        }
    }
}
