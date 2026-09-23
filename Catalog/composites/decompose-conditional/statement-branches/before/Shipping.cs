using System;
using System.Collections.Generic;

namespace Warehouse
{
    public class Shipping
    {
        private readonly List<string> _log = new List<string>();

        public void Ship(string parcel, int grams)
        {
            var label = parcel.ToUpperInvariant();
            /*^*/if (grams > 30000 && !label.StartsWith("LOCAL"))
            {
                // Freight needs a pallet.
                _log.Add("pallet");
                _log.Add("freight " + label);
            }
            else
            {
                _log.Add("post " + label); // cheapest
            }

            Console.WriteLine(label);
        }
    }
}
