namespace Shop
{
    public class Shipping
    {
        public decimal Rate(string zone)
        {
            var surcharge = 1m;
            if (zone == "local")
            {
                return 2.5m * surcharge;
            }
            else
            {
                /*^*/return 12m * surcharge;
            }
        }
    }
}
