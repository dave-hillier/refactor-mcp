namespace Shop
{
    public class Order
    {
        internal decimal _total;

        public string Currency { get; set; }

        public string Summary() => Receipts.Describe(this, "Order: ");

        public void Add(decimal price) => _total += price;
    }
}
