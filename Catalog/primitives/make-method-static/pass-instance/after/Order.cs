namespace Shop
{
    public class Order
    {
        private decimal _total;

        public string Currency { get; set; }

        /// <summary>A line for the receipt.</summary>
        public static string Describe(Order order, string prefix)
        {
            // The total is shown without rounding.
            return prefix + order._total + " " + order.Currency;
        }

        public string Summary() => Describe(this, "Order: ");

        public void Add(decimal price) => _total += price;
    }
}
