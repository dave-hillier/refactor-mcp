namespace Shop
{
    public class Shipping
    {
        public decimal Rate(string zone)
        {
            /*^*/if (zone == "local")
            {
                return 2.5m;
            }
            else if (zone == "national")
            {
                return 5m;
            }
            else
            {
                return 12m;
            }
        }
    }
}
