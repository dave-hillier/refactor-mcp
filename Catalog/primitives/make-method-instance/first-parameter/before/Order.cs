namespace Shop
{
    public class Order
    {
        public decimal Total { get; set; }

        // Rates are fractions, not percentages.
        public static decimal Discounted(Order order, decimal rate)
        {
            return order.Total * (1 - rate);
        }

        public decimal Compare(Order other) => Discounted(other, 0.2m) - Discounted(this, 0.2m);
    }
}
