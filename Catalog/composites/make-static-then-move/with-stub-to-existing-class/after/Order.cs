namespace Shop
{
    public class Order
    {
        internal decimal _total;

        public string Currency { get; set; }

        /// <summary>A line for the receipt.</summary>
        public static string Describe(Order sale, string prefix)
        {
            return Formatting.Describe(sale, prefix);
        }

        public string Summary() => Describe(this, "Order: ");

        public void Add(decimal price) => _total += price;
    }
}
