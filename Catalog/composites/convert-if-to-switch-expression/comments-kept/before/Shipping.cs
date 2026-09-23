namespace Shop
{
    public class Shipping
    {
        public decimal Rate(string zone)
        {
            // Zones are priced by distance.
            /*^*/if (zone == "local")
            {
                // Delivered the same day.
                return 2.5m;
            }
            else
            {
                return 12m; // by courier
            }
        }
    }
}
