namespace Shop
{
    public class Order
    {
        private decimal _total;

        public string Currency { get; set; }

        /// <summary>A line for the receipt.</summary>
        public string Describe(string prefix)
        {
            // The total is shown without rounding.
            return prefix + _total + " " + this.Currency;
        }

        public string Summary() => Describe("Order: ");

        public void Add(decimal price) => _total += price;
    }
}
