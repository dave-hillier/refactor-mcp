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
            if (IsHeavy(grams, label))
            {
                // Freight needs a pallet.
                ShipByFreight(label);
            }
            else
            {
                ShipByPost(label);
            }

            Console.WriteLine(label);
        }

        private bool IsHeavy(int grams, string label)
        {
            return grams > 30000 && !label.StartsWith("LOCAL");
        }

        private void ShipByFreight(string label)
        {
            _log.Add("pallet");
            _log.Add("freight " + label);
        }

        private void ShipByPost(string label)
        {
            _log.Add("post " + label); // cheapest
        }
    }
}
