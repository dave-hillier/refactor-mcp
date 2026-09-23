namespace Shop
{
    public class Pricing
    {
        private readonly decimal _markup = 1m + 0.2m;

        public decimal Price(decimal cost) => cost * _markup;

        public decimal Margin(decimal cost) => cost * _markup - cost;
    }
}
