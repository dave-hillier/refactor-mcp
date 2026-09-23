using System;

namespace Shipping
{
    public enum Zone
    {
        Domestic,
        Europe,
        World,
    }

    public class Rates
    {
        public decimal DomesticCost(decimal weight)
        {
            return weight * 1.5m;
        }

        public decimal EuropeCost(decimal weight)
        {
            return weight * 4m + 2m;
        }

        public decimal Quote(decimal weight)
        {
            return DomesticCost(weight) + EuropeCost(1m);
        }
    }
}
