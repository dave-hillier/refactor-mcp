namespace Shop
{
    public class Pricing
    {
        public decimal Price(decimal cost) => cost * (1m + 0.2m);

        public decimal Margin(decimal cost) => cost * (1m + 0.2m) - cost;
    }
}
