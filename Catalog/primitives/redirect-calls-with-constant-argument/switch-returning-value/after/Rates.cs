namespace Billing
{
    public enum Zone
    {
        Domestic,
        Europe,
        World,
    }

    public class Rates
    {
        public decimal Postage(Zone zone, decimal weight)
        {
            switch (zone)
            {
                case Zone.Domestic:
                    return Domestic(weight);
                case Zone.Europe:
                    return Europe(weight);
                default:
                    return weight * 5m;
            }
        }

        public decimal Domestic(decimal weight) => weight * 1m;

        public decimal Europe(decimal weight) => weight * 2m;

        public decimal Quote(decimal weight, Zone zone)
        {
            return Europe(weight) + Postage(zone, weight) + Postage(Zone.World, weight);
        }
    }
}
