namespace Shop
{
    public class Shipping
    {
        public decimal Rate(string zone)
        {
            return zone switch
            {
                "local" => 2.5m,
                "national" => 5m,
                _ => 12m,
            };
        }
    }
}
