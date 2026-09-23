namespace Shop
{
    public class Order
    {
        public decimal Total { get; set; }

        // Rates are fractions, not percentages.
        public decimal Discounted(decimal rate)
        {
            return Total * (1 - rate);
        }

        public decimal Compare(Order other) => other.Discounted(0.2m) - Discounted(0.2m);
    }
}
