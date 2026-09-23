namespace Shop
{
    public class Shipping
    {
        public decimal Rate(string zone)
        {
            // Zones are priced by distance.
            return zone switch
            {
                // Delivered the same day.
                "local" => 2.5m,
                _ => 12m, // by courier
            };
        }
    }
}
