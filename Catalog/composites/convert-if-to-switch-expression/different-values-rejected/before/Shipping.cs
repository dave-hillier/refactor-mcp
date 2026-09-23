namespace Shop
{
    public class Shipping
    {
        public decimal Rate(string zone, string carrier)
        {
            /*^*/if (zone == "local")
            {
                return 2.5m;
            }
            else if (carrier == "courier")
            {
                return 20m;
            }
            else
            {
                return 12m;
            }
        }
    }
}
