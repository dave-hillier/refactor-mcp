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
        public decimal Cost(Zone zone, decimal weight)
        {
            switch (zone)
            {
                case Zone.Domestic:
                    return weight * 1.5m;
                case Zone.Europe:
                    return weight * 4m + 2m;
                default:
                    throw new ArgumentOutOfRangeException(nameof(zone));
            }
        }

        public decimal Quote(decimal weight)
        {
            return Cost(Zone.Domestic, weight) + Cost(Zone.Europe, 1m);
        }
    }
}
