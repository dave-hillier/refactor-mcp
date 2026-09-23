using System;

namespace Shop
{
    public class Converter
    {
        // The last rate used.
        private decimal _rate = 1.1m;

        public decimal Convert(decimal amount)
        {
            var rate = _rate;

            // Round to whole cents.
            return Math.Round(/*[*/amount * rate/*]*/, 2); // banker's rounding
        }
    }
}
